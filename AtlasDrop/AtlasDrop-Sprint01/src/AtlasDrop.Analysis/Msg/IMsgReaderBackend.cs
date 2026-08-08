namespace AtlasDrop.Analysis.Msg;

public interface IMsgReaderBackend
{
    MsgBackendResult Read(
        string filePath,
        CancellationToken cancellationToken);
}

public sealed record MsgBackendResult(
    string? Subject,
    string? Sender,
    string? RecipientsTo,
    string? RecipientsCc,
    DateTime? SentOn,
    string? BodyText,
    string? BodyHtml);
