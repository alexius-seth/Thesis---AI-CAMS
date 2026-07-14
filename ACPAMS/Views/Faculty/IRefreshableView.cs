using System.Threading.Tasks;

namespace ACPAMS
{
    /// <summary>
    /// Implemented by any page/tab that should reload its data every time
    /// MainWindow navigates to it — including returning to an already-cached
    /// instance via Back or a sidebar link, not just on first construction.
    /// MainWindow.ShowPage calls this after every content swap.
    /// </summary>
    public interface IRefreshableView
    {
        Task RefreshAsync();
    }
}
