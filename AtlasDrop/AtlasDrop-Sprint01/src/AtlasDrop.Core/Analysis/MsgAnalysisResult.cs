namespace AtlasDrop.Core.Analysis;

public sealed record MsgAnalysisResult(
    string Subject,
    string Sender,
    string RecipientsTo,
    string RecipientsCc,
    DateTime? SentOn,
    string BodyText,
    bool WasTruncated);
