using MoreLinq;
using NJsonSchema;
using NSwag;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OasInterfaceVerification.Tests
{
    public class VerifierTests
    {
        private readonly Verifier _verifier;

        public VerifierTests()
        {
            _verifier = new Verifier();
        }

        [Fact]
        public void MissingPathFails()
        {
            var contractDoc = new OpenApiDocument();
            contractDoc.Paths.Add("/api/foo", new());
            contractDoc.Paths.Add("/api/bar", new());

            var candidateDoc = new OpenApiDocument();
            candidateDoc.Paths.Add("/api/bar", new());
            candidateDoc.Paths.Add("/api/baz", new());

            var actual = _verifier.Verify(candidateDoc, contractDoc);

            Assert.Equal(
                new [] { new MissingPath("/api/foo") },
                actual
            );
        }

        [Fact]
        public void MissingHttpMethodFails()
        {
            var contractDoc = new OpenApiDocument();
            contractDoc.Paths.Add("/api/foo", new OpenApiPathItem()
            {
                ["get"] = new OpenApiOperation(),
                ["put"] = new OpenApiOperation()
            });

            var candidateDoc = new OpenApiDocument();
            candidateDoc.Paths.Add("/api/foo", new OpenApiPathItem()
            {
                ["get"] = new OpenApiOperation(),
                ["post"] = new OpenApiOperation()
            });

            var actual = _verifier.Verify(candidateDoc, contractDoc);

            Assert.Equal(
                    new[] { new MissingHttpMethod("/api/foo", "put") },
                    actual
                );
        }

        [Fact]
        public void MismatchedParametersFails()
        {
            var testParamPairs = new List<ParameterPair> { 
                new () { 
                    Contract  = new () { Name = "ShouldMatch",    Kind = OpenApiParameterKind.Query },
                    Candidate = new () { Name = "ShouldMatch",    Kind = OpenApiParameterKind.Query },
                }, new () {
                    Contract  = new () { Name = "NameMismatch",   Kind = OpenApiParameterKind.Query },
                    Candidate = new () { Name = "__NameMismatch", Kind = OpenApiParameterKind.Query },
                }, new () {
                    Contract  = new () { Name = "KindMismatch",   Kind = OpenApiParameterKind.Header },
                    Candidate = new () { Name = "KindMismatch",   Kind = OpenApiParameterKind.Query },
                },
            };

            //verify conformance

            var contractDoc = new OpenApiDocument();
            var contactOp = new OpenApiOperation();
            contractDoc.Paths.Add("/api/foo", new () { ["get"] = contactOp });
            testParamPairs
                .Select(p => p.Contract)
                .ForEach(contactOp.Parameters.Add);

            var candidateDoc = new OpenApiDocument();
            var candidateOp = new OpenApiOperation();
            candidateDoc.Paths.Add("/api/foo", new() { ["get"] = candidateOp });
            testParamPairs
                .Select(p => p.Candidate)
                .ForEach(candidateOp.Parameters.Add);

            var actualFailures = _verifier.Verify(candidateDoc, contractDoc);

            var expectedFailures = new VerificationFailure[] {
                new MissingParameter("/api/foo", "get", "NameMismatch", OpenApiParameterKind.Query),
                new MissingParameter("/api/foo", "get", "KindMismatch", OpenApiParameterKind.Header),
            };

            Assert.Equal(
                    expectedFailures.OrderBy(x => x.GetHashCode()),
                    actualFailures.OrderBy(x => x.GetHashCode())
                );
        }

        [Fact]
        public void ExcessRequirementParamOnCandidateFails()
        {
            var testParamPairs = new List<ParameterPair> {
                new () {
                    Contract  = new () { Name = "BothRequired", IsRequired = true },
                    Candidate = new () { Name = "BothRequired", IsRequired = true },
                }, new () {
                    Candidate = new () { Name = "NotRequiredOnCandicateWithNoMatchingParam", IsRequired = false },
                }, new () {
                    Candidate  = new () { Name = "RequiredOnCandicateWithNoMatchingParam", IsRequired = true, MaxItems = 12 },
                },
            };

            //verify conformance

            var contractDoc = new OpenApiDocument();
            var contactOp = new OpenApiOperation();
            contractDoc.Paths.Add("/api/foo", new() { ["get"] = contactOp });
            testParamPairs
                .Select(p => p.Contract)
                .Where(p => p != default)
                .ForEach(contactOp.Parameters.Add);

            var candidateDoc = new OpenApiDocument();
            var candidateOp = new OpenApiOperation();
            candidateDoc.Paths.Add("/api/foo", new() { ["get"] = candidateOp });
            testParamPairs
                .Select(p => p.Candidate)
                .ForEach(candidateOp.Parameters.Add);

            var actualFailures = _verifier.Verify(candidateDoc, contractDoc);

            var expectedFailures = new VerificationFailure[] {
                new ExcessRequiredParameter("/api/foo", "get", "RequiredOnCandicateWithNoMatchingParam", default),
            };

            Assert.Equal(
                    expectedFailures.OrderBy(x => x.GetHashCode()),
                    actualFailures.OfType<ExcessRequiredParameter>().OrderBy(x => x.GetHashCode())
                );
        }

        [Fact]
        public void IncompatibleParametersFails()
        {
            // TODO: account for complex types as parameters

            // TODO: consider checking treating additional parameter properties as constraints

            // TODO: consider treating candidates as compatible if superset of contract parameter constraints

            var testParamPairs = new List<ParameterPair> {
                new () {
                    Contract  = new () { Name = "ShouldMatch",       Type = JsonObjectType.Integer, IsRequired = false },
                    Candidate = new () { Name = "ShouldMatch",       Type = JsonObjectType.Integer, IsRequired = false },
                }, new () {                                          
                    Contract  = new () { Name = "TypeMismatch",      Type = JsonObjectType.Integer, IsRequired = false },
                    Candidate = new () { Name = "TypeMismatch",      Type = JsonObjectType.String,  IsRequired = false },
                }, new () {
                    Contract  = new () { Name = "IsRequireMismatch", Type = JsonObjectType.Integer, IsRequired = true },
                    Candidate = new () { Name = "IsRequireMismatch", Type = JsonObjectType.Integer, IsRequired = false },
                }, new () {
                    Contract  = new () { Name = "MaximumMismatch",   Type = JsonObjectType.Integer, IsRequired = false, Maximum = 1024 },
                    Candidate = new () { Name = "MaximumMismatch",   Type = JsonObjectType.Integer, IsRequired = false, Maximum = 2048 }, // perhaps should not fail since candidate is superset of contract
                }, new () {
                    Contract  = new () { Name = "MaxItemsMismatch",  Type = JsonObjectType.String,  IsRequired = false, MaxLength = 10 },
                    Candidate = new () { Name = "MaxItemsMismatch",  Type = JsonObjectType.String,  IsRequired = false, MaxLength = 20 }, // perhaps should not fail since candidate is superset of contract
                }, new () {
                    Contract  = new () { Name = "MaxLengthMismatch", Type = JsonObjectType.Array,   IsRequired = false, MaxItems = 10  },
                    Candidate = new () { Name = "MaxLengthMismatch", Type = JsonObjectType.Array,   IsRequired = false, MaxItems = 20  }, // perhaps should not fail since candidate is superset of contract
                },
            };

            //verify conformance

            var contractDoc = new OpenApiDocument();
            var contactOp = new OpenApiOperation();
            contractDoc.Paths.Add("/api/foo", new() { ["get"] = contactOp });
            testParamPairs
                .Select(p => p.Contract)
                .ForEach(contactOp.Parameters.Add);

            var candidateDoc = new OpenApiDocument();
            var candidateOp = new OpenApiOperation();
            candidateDoc.Paths.Add("/api/foo", new() { ["get"] = candidateOp });
            testParamPairs
                .Select(p => p.Candidate)
                .ForEach(candidateOp.Parameters.Add);

            var actualFailures = _verifier.Verify(candidateDoc, contractDoc);

            var expectedFailures = testParamPairs.Skip(1).Select(p =>
                new IncompatibleParameter("/api/foo", "get", p.Contract!.Name, default, new(p.Contract!), new(p.Candidate!))
            );

            Assert.Equal(
                    expectedFailures.OrderBy(x => x.GetHashCode()),
                    actualFailures.OrderBy(x => x.GetHashCode())
                );
        }

        [Fact]
        public void IncompatibleRequestBodyFails()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void IncompatibleResponsesFails()
        {
            throw new NotImplementedException();
        }

        public class ParameterPair
        { 
            public OpenApiParameter? Candidate { get; init; }
            public OpenApiParameter? Contract { get; init; }
        }
    }
}
