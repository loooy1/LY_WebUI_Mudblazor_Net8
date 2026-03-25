
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services
{
    public sealed class CyclicConfigStore : ICyclicConfigReader, ICyclicConfigWriter
    {
        private RcsConnectionConfig _current = new();
        public void Save(RcsConnectionConfig config)
        {
            _current = config.Clone();
        }

        RcsConnectionConfig ICyclicConfigReader.Get()
        {
            return _current.Clone();
        }
    }
}
