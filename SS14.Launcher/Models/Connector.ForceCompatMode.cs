using System;
using System.Runtime.InteropServices;
using Serilog;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace SS14.Launcher.Models;

public partial class Connector
{
    private static bool CheckForceCompatMode()
    {
        if (OperatingSystem.IsWindows())
        {
            // Qualcomm (Snapdragon/Adreno) devices have broken OpenGL drivers on Windows.
            // This is because they rely on Mesa's d3d12 driver, which is broken:
            // https://gitlab.freedesktop.org/mesa/mesa/-/issues/13936
            if (CheckIsQualcommDevice())
            {
                Log.Information(
                    "We appear to be on a Qualcomm device. Enabling compat mode due to broken OpenGL driver");
                return true;
            }
        }

        return false;
    }

    private static unsafe bool CheckIsQualcommDevice()
    {
        // Ideally we would check the OpenGL driver instead... but OpenGL is terrible so that's impossible.
        // Let's just check with DXGI instead.

        IDXGIFactory1* dxgiFactory;
        ThrowIfFailed(
            nameof(DirectX.CreateDXGIFactory1),
            DirectX.CreateDXGIFactory1(TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory1>(), (void**)&dxgiFactory));

        try
        {
            uint idx = 0;
            IDXGIAdapter* adapter;
            while (dxgiFactory->EnumAdapters(idx, &adapter) != DXGI.DXGI_ERROR_NOT_FOUND)
            {
                try
                {
                    DXGI_ADAPTER_DESC desc;
                    ThrowIfFailed("GetDesc", adapter->GetDesc(&desc));

                    var descString = ((ReadOnlySpan<char>)desc.Description).TrimEnd('\0');
                    if (descString.Contains("qualcomm", StringComparison.OrdinalIgnoreCase) ||
                        descString.Contains("snapdragon", StringComparison.OrdinalIgnoreCase) ||
                        descString.Contains("adreno", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                finally
                {
                    adapter->Release();
                }

                idx += 1;
            }
        }
        finally
        {
            dxgiFactory->Release();
        }

        return false;
    }

    private static void ThrowIfFailed(string methodName, HRESULT hr)
    {
        if (TerraFX.Interop.Windows.Windows.FAILED(hr))
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }
}
