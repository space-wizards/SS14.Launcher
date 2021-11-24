using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class MainArgs : IMainArgs
{
    public MainArgs(string[] args, IFileApi fileApi, IRedialApi? redialApi)
    {
        Args = args;
        FileApi = fileApi;
        RedialApi = redialApi;
    }

    public string[] Args { get; }
    public IFileApi FileApi { get; }
    public IRedialApi? RedialApi { get; }
}
