using System.Linq;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers;

namespace Selu383Bot.GithubWebhook;

public class JsonNetSerializer : IRestSerializer, ISerializer, IDeserializer
{
    private static readonly string[] Types = {
        "application/json", "text/json", "text/x-json", "text/javascript", "*+json"
    };

    public string Serialize(Parameter parameter) => Serialize(parameter.Value);

    public ISerializer Serializer => this;
    public IDeserializer Deserializer => this;

    public string[] AcceptedContentTypes => Types;

    public SupportsContentType SupportsContentType { get; } = x => Types.Contains(x);

    public DataFormat DataFormat => DataFormat.Json;

    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public string ContentType { get; set; } = Types[0];

    public T Deserialize<T>(RestResponse response)
    {
        if (response.Content == null)
        {
            return default;
        }
        return JsonConvert.DeserializeObject<T>(response.Content);
    }
}