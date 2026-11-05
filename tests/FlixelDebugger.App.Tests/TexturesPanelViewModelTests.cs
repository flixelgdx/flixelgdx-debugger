// MIT License
//
// Copyright (c) 2026 stringdotjar
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using FlixelDebugger.App.Tests.Fakes;
using FlixelDebugger.Core.Data;
using FlixelDebugger.Core.Sessions;
using FlixelDebugger.Protocol.Panels;

namespace FlixelDebugger.App.Tests;

public class TexturesPanelViewModelTests
{
    private static TexturesListPayload Sample() => TestData.Textures(
        TestData.Texture(1, "b.png", 32, 8, 2 * 1024 * 1024, users: 3),
        TestData.Texture(2, "a.png", 16, 16, 1024 * 1024, users: 1, duplicate: true),
        TestData.Texture(3, "a.png", 64, 64, 4 * 1024 * 1024, users: 2, duplicate: true));

    [Fact]
    public void ShowsTheTotalsAndOneRowPerTexture()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();

        var vm = f.Textures();

        Assert.Equal("Textures: 3", vm.CountText);
        Assert.Equal("GPU: 7.00 MB", vm.GpuText);
        Assert.Equal("Duplicates: 2", vm.DuplicatesText);
        Assert.Equal([1, 2, 3], vm.Rows.Select(r => r.Id));
        Assert.Equal("32x8", vm.Rows[0].SizeText);
        Assert.Equal("2.00", vm.Rows[0].GpuMbText);
        Assert.Equal("RGBA8", vm.Rows[0].Format);
        Assert.False(vm.Rows[0].IsDuplicate);
        Assert.True(vm.Rows[1].IsDuplicate);
        Assert.Equal("Duplicate", vm.Rows[1].DuplicateText);
    }

    [Fact]
    public void WithoutATextureListTheTotalsAreDashes()
    {
        var vm = new PanelFixture().Textures();

        Assert.Equal("Textures: -", vm.CountText);
        Assert.Equal("0 of 0", vm.PageText);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void SortingByAColumnOrdersTheRowsAndTheSameColumnAgainFlipsIt()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();

        vm.SortBy(TextureSortKey.Name);
        Assert.Equal([2, 3, 1], vm.Rows.Select(r => r.Id));

        vm.SortBy(TextureSortKey.Name);
        Assert.True(vm.SortDescending);
        Assert.Equal([1, 2, 3], vm.Rows.Select(r => r.Id));

        vm.SortBy(TextureSortKey.GpuBytes);
        Assert.False(vm.SortDescending);
        Assert.Equal([2, 1, 3], vm.Rows.Select(r => r.Id));

        vm.SortBy(TextureSortKey.Size);
        Assert.Equal([1, 2, 3], vm.Rows.Select(r => r.Id));

        vm.SortBy(TextureSortKey.Users);
        Assert.Equal([2, 3, 1], vm.Rows.Select(r => r.Id));
    }

    [Fact]
    public void TheHeaderOfTheSortedColumnShowsAnArrow()
    {
        var f = new PanelFixture();
        var vm = f.Textures();

        vm.SortByColumnCommand.Execute(TextureSortKey.Users);

        Assert.EndsWith("▲", vm.Headers[(int)TextureSortKey.Users].Text);
        Assert.Equal("Name or path", vm.Headers[(int)TextureSortKey.Name].Text);

        vm.SortByColumnCommand.Execute(TextureSortKey.Users);

        Assert.EndsWith("▼", vm.Headers[(int)TextureSortKey.Users].Text);
    }

    [Fact]
    public void SelectingARowAsksForItsThumbnailOnce()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();

        vm.SelectedRow = vm.Rows[1];
        f.Tick();
        f.Tick();

        Assert.Equal(["textureThumbnail:2"], f.Game.Actions);
    }

    [Fact]
    public void ARefreshOfTheListKeepsTheSelectionAndSendsNoRequest()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SelectedRow = vm.Rows[2];

        f.Game.Textures = Sample();
        f.Tick();

        Assert.Equal(3, vm.SelectedRow?.Id);
        Assert.Equal(["textureThumbnail:3"], f.Game.Actions);
    }

    [Fact]
    public void ChangingTheSortKeepsTheSelectionAndSendsNoRequest()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SelectedRow = vm.Rows[0];

        vm.SortBy(TextureSortKey.Name);

        Assert.Equal(1, vm.SelectedRow?.Id);
        Assert.Equal(["textureThumbnail:1"], f.Game.Actions);
    }

    [Fact]
    public void ATextureThatLeavesTheListDeselects()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SelectedRow = vm.Rows[2];

        f.Game.Textures = TestData.Textures(TestData.Texture(1));
        f.Tick();

        Assert.Null(vm.SelectedRow);
        Assert.False(vm.Preview.HasImage);
    }

    [Fact]
    public void TheThumbnailAppearsInThePreviewWhenItArrives()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SelectedRow = vm.Rows[0];
        Assert.False(vm.Preview.HasImage);

        f.Game.TextureThumbnails.Set(new Thumbnail(1, 32, 8, TestData.Png));
        f.Tick();

        Assert.True(vm.Preview.HasImage);
        Assert.Equal(32, vm.Preview.ImageWidth);
    }

    [Fact]
    public void ThePreviewGivesUpWhenTheGameNeverAnswers()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SelectedRow = vm.Rows[0];

        for (var i = 0; i < 35; i++)
        {
            f.Tick();
        }

        Assert.Equal("The game did not send a preview.", vm.Preview.Status);
    }

    [Fact]
    public void ThePagerWalksThroughTheTexturesInTheSortedOrder()
    {
        var f = new PanelFixture();
        f.Game.Textures = Sample();
        var vm = f.Textures();
        vm.SortBy(TextureSortKey.Name);
        Assert.Equal("0 of 3", vm.PageText);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));

        vm.SelectedRow = vm.Rows[0];
        Assert.Equal("1 of 3", vm.PageText);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.True(vm.NextCommand.CanExecute(null));

        vm.NextCommand.Execute(null);
        Assert.Equal(3, vm.SelectedRow?.Id);
        Assert.Equal("2 of 3", vm.PageText);

        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.SelectedRow?.Id);
        Assert.Equal("3 of 3", vm.PageText);
        Assert.False(vm.NextCommand.CanExecute(null));

        vm.PreviousCommand.Execute(null);
        Assert.Equal(3, vm.SelectedRow?.Id);
        Assert.Equal(["textureThumbnail:2", "textureThumbnail:3", "textureThumbnail:1", "textureThumbnail:3"], f.Game.Actions);
    }
}
