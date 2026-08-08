namespace AtlasDrop.Core.Search;

public interface IFuzzyMatcher
{
    bool IsMatch(
        string candidate,
        string query,
        out double similarity);
}
