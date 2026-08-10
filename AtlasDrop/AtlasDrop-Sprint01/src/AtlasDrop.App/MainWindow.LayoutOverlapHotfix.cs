using System;
using System.Windows.Controls;

namespace AtlasDrop.App;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Le bloc central doit pouvoir se réduire quand le pied de fenêtre prend plus de place.
        // Les MinHeight XAML fixes forçaient le bloc "NOM PROPOSÉ" sous le footer.
        if (SuggestionPanel.Parent is Grid contentGrid && contentGrid.RowDefinitions.Count >= 2)
            contentGrid.RowDefinitions[0].MinHeight = 0;

        SuggestionPanel.MinHeight = 0;
        MainContentScrollViewer.MinHeight = 220;
    }
}
