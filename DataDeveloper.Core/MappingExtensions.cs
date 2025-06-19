using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicData;

namespace DataDeveloper;

public static class MappingExtensions
{
    #region Serializer config
    public static JsonSerializerOptions GetJsonSerializerOptions(params JsonConverter[] converters)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        jsonSerializerOptions.Converters.AddRange(converters);
        return jsonSerializerOptions;
    }

    private static string SerializerObjectToJson(object obj, JsonSerializerOptions? serializerOptions = null)
    {
        var convertObjToJson = JsonSerializer.Serialize(obj, serializerOptions ?? GetJsonSerializerOptions());

        return convertObjToJson;
    }

    private static T DeserializerJsonToObject<T>(string obj, JsonSerializerOptions? serializerOptions = null)
    {
        var convertJsonToObj = JsonSerializer.Deserialize<T>(obj, serializerOptions ?? GetJsonSerializerOptions());
        return convertJsonToObj;
    }
    #endregion

    // public static T Map<T>(this object obj)
    // {
    //     object objCurrent = null;
    //     try
    //     {
    //         object response = null;
    //         if (obj != null)
    //         {
    //             var typeObj = typeof(T);
    //             var fullName = typeObj.FullName;
    //             Assembly assem = typeObj.Assembly;
    //
    //             dynamic convertJsonToObj = null;
    //             var convertToJson = SerializerObjectToJson(obj);
    //             if (!string.IsNullOrEmpty(convertToJson))
    //             {
    //                 //objCurrent = assem.CreateInstance(fullName);
    //                 convertJsonToObj = DeserializerJsonToObject<T>(convertToJson);
    //                 if (convertJsonToObj != null)
    //                 {
    //                     objCurrent = convertJsonToObj;
    //                 }
    //
    //                 response = (T)Convert.ChangeType(objCurrent, typeof(T));
    //             }
    //         }
    //
    //         return (T)response;
    //     }
    //     catch (InvalidCastException)
    //     {
    //         return default(T);
    //     }
    // }

    public static T Map<T>(this object value, params object[] extraValues)
    {
        return Map<T>(value, null, extraValues);
    }

    public static T Map<T>(this object value, JsonSerializerOptions? serializerOptions, params object[] extraValues)
    {
        try
        {
            var response = default(T);
            
            if (value == null) return response;
            
            var convertToJson = SerializerObjectToJson(value, serializerOptions);

            if (extraValues != null)
            {
                if (extraValues.Length > 0)
                {

                    var extraValuesJson = extraValues.Select(eo => eo == null ? "{}" : SerializerObjectToJson(eo, serializerOptions));
                    var extraValuesDictionary = extraValuesJson.SelectMany(eoj => DeserializerJsonToObject<Dictionary<string, object>>(eoj, serializerOptions));
                    var finalDictionary = DeserializerJsonToObject<Dictionary<string, object>>(convertToJson, serializerOptions);

                    foreach (var keyValuePair in extraValuesDictionary)
                    {
                        var added = finalDictionary.TryAdd(keyValuePair.Key, keyValuePair.Value);
                    }

                    return Map<T>(finalDictionary, serializerOptions, null);
                }
            }

            if (!string.IsNullOrEmpty(convertToJson))
            {
                response = DeserializerJsonToObject<T>(convertToJson, serializerOptions);
            }

            return response;
        }
        catch (InvalidCastException)
        {
            return default(T);
        }
    }  
}