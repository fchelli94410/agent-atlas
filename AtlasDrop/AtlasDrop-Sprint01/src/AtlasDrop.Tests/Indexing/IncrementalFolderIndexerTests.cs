using Xunit;
using AtlasDrop.Core.Indexing;
using AtlasDrop.Infrastructure.Indexing;
namespace AtlasDrop.Tests.Indexing;
public sealed class IncrementalFolderIndexerTests
{
    private static readonly DateTime T = new(2026,8,7,10,0,0,DateTimeKind.Utc);
    [Fact] public void New_folder_is_added(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\Root\A",T,0,new[]{".pdf"})},Array.Empty<IndexedFolderSnapshot>());Assert.Single(r.Added);Assert.Empty(r.Changed);Assert.Empty(r.Removed);}
    [Fact] public void Missing_folder_is_removed(){var r=new IncrementalFolderIndexer().Compare(Array.Empty<FolderScanItem>(),new[]{Snap(@"C:\Root\A",T,0,new[]{".pdf"})});Assert.Single(r.Removed);}
    [Fact] public void Modified_date_marks_changed(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\Root\A",T.AddMinutes(1),0,new[]{".pdf"})},new[]{Snap(@"C:\Root\A",T,0,new[]{".pdf"})});Assert.Single(r.Changed);}
    [Fact] public void File_count_marks_changed(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\Root\A",T,2,new[]{".pdf"})},new[]{Snap(@"C:\Root\A",T,1,new[]{".pdf"})});Assert.Single(r.Changed);}
    [Fact] public void Extension_change_marks_changed(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\Root\A",T,1,new[]{".docx"})},new[]{Snap(@"C:\Root\A",T,1,new[]{".pdf"})});Assert.Single(r.Changed);}
    [Fact] public void Same_folder_is_unchanged(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\Root\A",T,2,new[]{".pdf",".txt"})},new[]{Snap(@"C:\Root\A",T,2,new[]{".TXT",".PDF"})});Assert.Single(r.Unchanged);Assert.Empty(r.Added);Assert.Empty(r.Changed);Assert.Empty(r.Removed);}
    [Fact] public void Paths_are_case_insensitive(){var r=new IncrementalFolderIndexer().Compare(new[]{Item(@"C:\ROOT\A",T,0,Array.Empty<string>())},new[]{Snap(@"c:\root\a",T,0,Array.Empty<string>())});Assert.Single(r.Unchanged);}
    [Fact] public void Mixed_changes_are_classified(){var cur=new[]{Item(@"C:\Root\Keep",T,0,Array.Empty<string>()),Item(@"C:\Root\Change",T.AddMinutes(2),1,new[]{".pdf"}),Item(@"C:\Root\New",T,0,Array.Empty<string>())};var old=new[]{Snap(@"C:\Root\Keep",T,0,Array.Empty<string>()),Snap(@"C:\Root\Change",T,1,new[]{".pdf"}),Snap(@"C:\Root\Old",T,0,Array.Empty<string>())};var r=new IncrementalFolderIndexer().Compare(cur,old);Assert.Single(r.Added);Assert.Single(r.Changed);Assert.Single(r.Removed);Assert.Single(r.Unchanged);}
    private static FolderScanItem Item(string p,DateTime m,int c,IReadOnlyCollection<string> e)=>new(p,Path.GetFileName(p),Path.GetDirectoryName(p),1,T,m,c,e);
    private static IndexedFolderSnapshot Snap(string p,DateTime m,int c,IReadOnlyCollection<string> e)=>new(p,m,c,e);
}
