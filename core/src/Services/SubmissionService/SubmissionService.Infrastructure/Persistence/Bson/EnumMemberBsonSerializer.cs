using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Reflection;
using System.Runtime.Serialization;

namespace SubmissionService.Infrastructure.Persistence.Bson;

internal sealed class EnumMemberBsonSerializer<TEnum> : SerializerBase<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> EnumToString = BuildEnumToStringMap();
    private static readonly Dictionary<string, TEnum> StringToEnum = BuildStringToEnumMap();

    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        switch (context.Reader.CurrentBsonType)
        {
            case BsonType.String:
                var value = context.Reader.ReadString();
                if (StringToEnum.TryGetValue(value, out var enumValue))
                {
                    return enumValue;
                }

                throw new FormatException($"Unknown {typeof(TEnum).Name} value '{value}'.");

            case BsonType.Null:
                context.Reader.ReadNull();
                return default;

            default:
                throw new FormatException(
                    $"Cannot deserialize {typeof(TEnum).Name} from BsonType {context.Reader.CurrentBsonType}.");
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
    {
        if (EnumToString.TryGetValue(value, out var stringValue))
        {
            context.Writer.WriteString(stringValue);
            return;
        }

        throw new FormatException($"Unknown {typeof(TEnum).Name} value '{value}'.");
    }

    private static Dictionary<TEnum, string> BuildEnumToStringMap()
    {
        return Enum.GetValues<TEnum>()
            .ToDictionary(
                enumValue => enumValue,
                enumValue => GetEnumMemberValue(enumValue));
    }

    private static Dictionary<string, TEnum> BuildStringToEnumMap()
    {
        return EnumToString.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.Ordinal);
    }

    private static string GetEnumMemberValue(TEnum value)
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var enumMember = member?.GetCustomAttribute<EnumMemberAttribute>();
        return enumMember?.Value ?? value.ToString().ToLowerInvariant();
    }
}
