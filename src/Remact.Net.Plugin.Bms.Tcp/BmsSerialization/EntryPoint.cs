//===========================================================================//
//                                     ___      _   ___          _           //
//   Mettler Toledo                   / _ \___ | | / (_)__  ____(_)          //
//                                   / // / _ `/ |/ / / _ \/ __/ /           //
//                                  /____/\_,_/|___/_/_//_/\__/_/            //
//                                                                           //
//===========================================================================//

namespace MT.DaVinci.Gui.SystemTest.Service
{
    using System;

    /// <summary>
    /// Entry point for the dynamic loadable assembly.
    /// The default constructor of this class initializes the UI system test service.
    /// </summary>
    public sealed class EntryPoint : IDisposable
    {
        private GuiSystemTestService _guiSystemTestService;

        /// <summary>
        /// Default constructor is called when assembly is loaded and initialized from BusinessLogic.WebService.
        /// </summary>
        public EntryPoint()
        {
            _guiSystemTestService = new GuiSystemTestService();
        }


        public void Dispose()
        {
            if (_guiSystemTestService != null)
            {
                _guiSystemTestService.Dispose();
                _guiSystemTestService = null;
            }
        }
    }
}