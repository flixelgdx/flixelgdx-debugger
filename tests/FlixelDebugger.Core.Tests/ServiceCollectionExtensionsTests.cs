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

using FlixelDebugger.Core.Adb;
using FlixelDebugger.Core.Server;
using FlixelDebugger.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace FlixelDebugger.Core.Tests;

public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "flixelgdx-debugger-di-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task RegistersTheCoreServicesAsSingletons()
    {
        var path = Path.Combine(_folder, "settings.json");
        new AppSettings { Port = 7123, ListenOnAllInterfaces = true }.Save(path);

        await using var provider = new ServiceCollection().AddFlixelDebuggerCore(path).BuildServiceProvider();

        var server = provider.GetRequiredService<DebugServer>();
        Assert.Same(server, provider.GetRequiredService<DebugServer>());
        Assert.Same(provider.GetRequiredService<AppSettings>(), provider.GetRequiredService<AppSettings>());
        Assert.NotNull(provider.GetRequiredService<AdbHelper>());
        Assert.Equal(7123, server.Options.Port);
        Assert.True(server.Options.ListenOnAllInterfaces);
    }

    [Fact]
    public void KeepsServicesRegisteredEarlier()
    {
        var custom = new DebugServerOptions { Port = 9 };
        var services = new ServiceCollection().AddSingleton(custom).AddFlixelDebuggerCore(Path.Combine(_folder, "none.json"));

        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<DebugServerOptions>());
    }
}
