/*
 * UIViewModel.cs - part of CNC Controls library for Grbl
 *
 * v0.27 / 2020-09-19 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{

    //public class CNCView
    //{
    //    public string Label { get; set; }
    //    public ICNCView View { get; set; } 
    //}

    public class UIViewModel : ViewModelBase
    {
        private ICNCView _currentView = null;

        public UIViewModel()
        {
            //CNCViews = new ObservableCollection<CNCView>();
            SidebarItems = new ObservableCollection<SidebarItem>();
            ConfigControls = new ObservableCollection<UserControl>();
        }

        public ICamera Camera { get; set; }
        public ConsoleWindow Console { get; set; }
        //public ObservableCollection<CNCView> CNCViews { get;  }
        public ObservableCollection<SidebarItem> SidebarItems { get; }
        public ObservableCollection<UserControl> ConfigControls { get; }
        public ObservableCollection<MenuItem> TransformMenuItems { get; } = new ObservableCollection<MenuItem>();

        public bool IsConfigControlInstantiated<T>()
        {
            return ConfigControls.OfType<T>().FirstOrDefault() != null;
        }

        public ICNCView CurrentView
        {
            get { return _currentView; }
            set
            {
                if (value != _currentView)
                {
                    _currentView = value;
                    foreach (SidebarItem cmd in SidebarItems)
                    {
                        cmd.IsEnabled = _currentView.ViewType == ViewType.GRBL || _currentView.ViewType == ViewType.Probing;
                        cmd.Visibility = Visibility.Hidden;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentViewType));
                }
            }
        }

        public ViewType CurrentViewType { get { return _currentView == null ? ViewType.Startup : _currentView.ViewType; } }
    }
}
