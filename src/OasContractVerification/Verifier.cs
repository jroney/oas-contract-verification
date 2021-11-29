using NJsonSchema;
using NSwag;

namespace OasInterfaceVerification
{
    public class Verifier
    {
        public IEnumerable<VerificationFailure> Verify(OpenApiDocument candidate, OpenApiDocument contract)
        {
            foreach (var contractPathEntry in contract.Paths)
            {
                if (candidate.Paths.TryGetValue(contractPathEntry.Key, out var candPathItem))
                {
                    var cx = new VerificationContext(Path: contractPathEntry.Key);
                    var failures = VerifyOperations(cx, candPathItem, contractPathEntry.Value);
                    foreach (var failure in failures) 
                        yield return failure;
                }
                else
                {
                    yield return new MissingPath(contractPathEntry.Key);
                }
            }
        }

        private IEnumerable<VerificationFailure> VerifyOperations(VerificationContext cx, OpenApiPathItem candidate, OpenApiPathItem contract)
        {
            foreach (KeyValuePair<string, OpenApiOperation> contractOperationEntry in contract)
            {
                cx = cx with { HttpMethod = contractOperationEntry.Key };
                if (candidate.TryGetValue(contractOperationEntry.Key, out var candidateOperation))
                {
                    var failures = VerifyOperation(cx, candidateOperation, contractOperationEntry.Value);
                    foreach (var failure in failures)
                        yield return failure;
                }
                else
                {
                    yield return new MissingHttpMethod(cx.Path, cx.HttpMethod);
                }
            }
        }

        private IEnumerable<VerificationFailure> VerifyOperation(VerificationContext cx, OpenApiOperation candidateOperation, OpenApiOperation contractOperation)
        {
            return VerifyParameters(cx, candidateOperation, contractOperation)
                //
                // TODO: uncomment below when implemented
                //
                //.Concat(VerifyRequestBody(cx, candidateOperation, contractOperation))
                //.Concat(VerifyResponse(cx, candidateOperation, contractOperation))
                ;
        }

        private IEnumerable<VerificationFailure> VerifyParameters(VerificationContext cx, OpenApiOperation candidateOperation, OpenApiOperation contractOperation)
        {
            var candidateParamMap = candidateOperation.Parameters.ToDictionary(p => (p.Name, p.Kind));
            var contractParamMap = contractOperation.Parameters.ToDictionary(p => (p.Name, p.Kind));

            var missingParamFailures = contractParamMap.Keys
                .Except(candidateParamMap.Keys)
                .Select(x => new MissingParameter(cx.Path, cx.HttpMethod, x.Name, x.Kind));

            var excessRequiredParameters = candidateParamMap.Keys
                .Except(contractParamMap.Keys)
                .Select(x => candidateParamMap[x])
                .Where(p => p.IsRequired)
                .Select(p => new ExcessRequiredParameter(cx.Path, cx.HttpMethod, p.Name, p.Kind));

            var incompatibleParameters = candidateParamMap
                .Join(contractParamMap, candKv => candKv.Key, contKv => contKv.Key, (candKv, contKv) => (candParam: candKv.Value, contParam: contKv.Value))
                .SelectMany(x => VerifyParameterCompatibility(cx, x.candParam, x.contParam));

            return Enumerable.Empty<VerificationFailure>()
                             .Concat(missingParamFailures)
                             .Concat(excessRequiredParameters)
                             .Concat(incompatibleParameters);
        }

        private IEnumerable<VerificationFailure> VerifyParameterCompatibility(VerificationContext cx, OpenApiParameter candidateParameter, OpenApiParameter contractParameter)
        {
            var candStub = new ParameterConstraints(candidateParameter);
            var contStub = new ParameterConstraints(contractParameter);

            if (!candStub.Equals(contStub))
            {
                yield return new IncompatibleParameter(cx.Path, cx.HttpMethod, contractParameter.Name, contractParameter.Kind, contStub, candStub);
            }
        }

        private IEnumerable<VerificationFailure> VerifyRequestBody(VerificationContext cx, OpenApiOperation candidateOperation, OpenApiOperation contractOperation)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<VerificationFailure> VerifyResponse(VerificationContext cx, OpenApiOperation candidateOperation, OpenApiOperation contractOperation)
        {
            throw new NotImplementedException();
        }

        private record VerificationContext(string Path, string HttpMethod = "");
    }

    //

    public abstract record VerificationFailure(string Path);

    public record MissingPath(string Path) : VerificationFailure(Path);

    public record MissingHttpMethod(string Path, string Method) : VerificationFailure(Path);

    public record MissingParameter(string Path, string Method, string ParameterName, OpenApiParameterKind ParameterKind) : VerificationFailure(Path);

    public record ExcessRequiredParameter(string Path, string Method, string ParameterName, OpenApiParameterKind ParameterKind) : VerificationFailure(Path);

    public record IncompatibleParameter(string Path, string Method, string ParameterName, OpenApiParameterKind ParameterKind, ParameterConstraints ContractParameterConstraints, ParameterConstraints CandidateParameterConstraints) : VerificationFailure(Path);

    public record ParameterConstraints
    {
        public ParameterConstraints(OpenApiParameter parameter): 
            this(
                parameter.Type,
                parameter.IsRequired,
                parameter.Maximum,
                parameter.MaxItems,
                parameter.MaxLength
            ) { }

        public ParameterConstraints(JsonObjectType type, bool isRequired, decimal? maximum, int maxItems, int? maxLength)
        {
            Type       = type;
            IsRequired = isRequired;
            Maximum    = maximum;
            MaxItems   = maxItems;
            MaxLength  = maxLength;
        }

        public JsonObjectType Type { get; }
        public bool IsRequired { get; }
        public decimal? Maximum { get; }
        public int MaxItems { get; }
        public int? MaxLength { get; }
    }
}