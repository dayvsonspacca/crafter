using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Crafter.Desktop.Controls;

namespace Crafter.Desktop.Tests.Controls;

public sealed class CraftingCellsTest
{
    [Fact]
    public void ADoneAnswerLightsItsRingAndLeavesTheCentreOut()
    {
        Assert.All([0, 1, 2, 3, 5, 6, 7, 8], cell => Assert.Equal((1, 0), CraftingCells.Shade(CellsMotion.Wave, crafting: false, cell, 0)));
        Assert.Equal((0, 0), CraftingCells.Shade(CellsMotion.Wave, crafting: false, 4, 0));
    }

    [Fact]
    public void TheWavePulsesEachCellALittleAfterTheOneBefore()
    {
        Assert.Equal(1, CraftingCells.Shade(CellsMotion.Wave, crafting: true, 0, 0).Lit);
        Assert.Equal(0, CraftingCells.Shade(CellsMotion.Wave, crafting: true, 0, 0.6).Lit, precision: 6);
        Assert.Equal(1, CraftingCells.Shade(CellsMotion.Wave, crafting: true, 0, 1.2).Lit, precision: 6);
        Assert.Equal(0, CraftingCells.Shade(CellsMotion.Wave, crafting: true, 8, 0.5).Lit);
    }

    [Fact]
    public void TheBenchFillsItsRingThenFoldsIntoTheCentre()
    {
        Assert.All(Enumerable.Range(0, 9), cell => Assert.Equal((0, 0), CraftingCells.Shade(CellsMotion.Bench, crafting: true, cell, 0)));

        // Halfway, the whole ring is in and the centre still empty.
        Assert.All([0, 1, 2, 3, 5, 6, 7, 8], cell => Assert.Equal(1, CraftingCells.Shade(CellsMotion.Bench, crafting: true, cell, 1.2).Lit));
        Assert.Equal((0, 0), CraftingCells.Shade(CellsMotion.Bench, crafting: true, 4, 1.2));

        // Then the ring is gone and the centre holds the result.
        Assert.All([0, 1, 2, 3, 5, 6, 7, 8], cell => Assert.Equal(0, CraftingCells.Shade(CellsMotion.Bench, crafting: true, cell, 1.92).Lit));
        Assert.Equal((1, 0), CraftingCells.Shade(CellsMotion.Bench, crafting: true, 4, 1.92));
    }

    [Fact]
    public void TicksOnlyWhileCraftingOnScreen()
    {
        TestApp.Run(() =>
        {
            var cells = new CraftingCells { Width = 19, Height = 19 };
            var window = new Window { Content = cells };
            window.Show();
            Settle();
            Assert.False(cells.IsTicking);

            cells.IsCrafting = true;
            Assert.True(cells.IsTicking);

            window.Content = null;
            Settle();
            Assert.False(cells.IsTicking);

            window.Content = cells;
            Settle();
            Assert.True(cells.IsTicking);

            cells.IsCrafting = false;
            Assert.False(cells.IsTicking);
            window.Close();
        });
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
