namespace AtlasDrop.Core.Naming;

public interface IFileRenameSuggestionService
{
    FileRenameSuggestion Suggest(FileRenameContext context);
}
