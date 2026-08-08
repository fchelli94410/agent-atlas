using System.Reflection;
using MsgReader.Outlook;

namespace AtlasDrop.Analysis.Msg;

public sealed class MsgReaderBackend : IMsgReaderBackend
{
    public MsgBackendResult Read(
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var message = new Storage.Message(filePath);

        cancellationToken.ThrowIfCancellationRequested();

        var type = message.GetType();

        string? subject = ReadString(type, message, "Subject");
        string? sender = ReadString(type, message, "Sender");
        DateTime? sentOn = ReadDateTime(type, message, "SentOn");

        string? bodyText =
            ReadString(type, message, "BodyText") ??
            ReadString(type, message, "Body");

        string? bodyHtml =
            ReadString(type, message, "BodyHtml");

        string? recipientsTo = TryGetRecipients(message, type, "To");
        string? recipientsCc = TryGetRecipients(message, type, "Cc");

        return new MsgBackendResult(
            subject,
            sender,
            recipientsTo,
            recipientsCc,
            sentOn,
            bodyText,
            bodyHtml);
    }

    private static string? ReadString(
        Type type,
        object instance,
        string propertyName)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        return property?.GetValue(instance)?.ToString();
    }

    private static DateTime? ReadDateTime(
        Type type,
        object instance,
        string propertyName)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        var value = property?.GetValue(instance);

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            _ => null
        };
    }

    private static string? TryGetRecipients(
        object message,
        Type type,
        string recipientTypeName)
    {
        try
        {
            var method = type.GetMethod(
                "GetEmailRecipients",
                BindingFlags.Instance | BindingFlags.Public);

            if (method is null)
                return null;

            var parameters = method.GetParameters();
            if (parameters.Length != 3)
                return null;

            var enumType = parameters[0].ParameterType;
            var enumValue = Enum.Parse(
                enumType,
                recipientTypeName,
                ignoreCase: true);

            return method.Invoke(
                message,
                new object?[] { enumValue, false, false })?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
