using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace Cwseo.NINA.Focuser.FocuserDockables {
    [Export(typeof(IDockableVM))]
    public class FocuserDockable : DockableVM, IFocuserConsumer{
        private IFocuserMediator focuserMediator;
        private IImagingMediator imaging;
        private ICameraMediator Camera;
        private CancellationTokenSource FocusControlToken;
        private int _userstep = 5;
        private int _targetpos = 100;
        private bool _moving = false;

        public int TargetPos {
            get => _targetpos;
            set {
                _targetpos = value;
                RaisePropertyChanged(nameof(TargetPos));
            }
        }

        public int UserStep {
            get => _userstep;
            set {
                _userstep = value;
                RaisePropertyChanged(nameof(UserStep));
            }
        }
        public bool Moving {
            get => _moving;
            set {
                _moving = value;
                RaisePropertyChanged(nameof(Moving));
            }
        }

        public RelayCommand StopFocusControl { get; set; }
        public RelayCommand MoveToPosition { get; set; }
        public RelayCommand MoveIN { get; set; }
        public RelayCommand MoveOUT { get; set; }

        // tool icon position setting
        override public bool IsTool { get; } = true;

        public void Dispose() {
            // On shutdown cleanup
            focuserMediator.RemoveConsumer(this);
        }
        public FocuserInfo FocuserInfo { get; private set; }
        public TelescopeInfo TelescopeInfo { get; private set; }
        public DeepSkyObject Target { get; private set; }

        public void UpdateDeviceInfo(FocuserInfo deviceInfo) {
            // The IsVisible flag indicates if the dock window is active or hidden
            if (IsVisible) {
                FocuserInfo = deviceInfo;
                RaisePropertyChanged(nameof(FocuserInfo));
            }
        }

        public void UpdateEndAutoFocusRun(AutoFocusInfo info) {
            throw new NotImplementedException();
        }

        public void UpdateUserFocused(FocuserInfo info) {
            throw new NotImplementedException();
        }

        [ImportingConstructor]
        public FocuserDockable(IProfileService profileService, ICameraMediator Camera, IImagingMediator imaging, IFocuserMediator focuser) : base(profileService) {

            // This will reference the resource dictionary to import the SVG graphic and assign it as the icon for the header bar
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Cwseo.NINA.Focuser;component/FocuserDockables/FocuserDockableTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Cwseo.NINA.Manualfocuser_SVG"];
            ImageGeometry.Freeze();

            this.focuserMediator = focuser;
            this.imaging = imaging;
            this.Camera = Camera;
            Target = null;
            focuserMediator.RegisterConsumer(this);   // ← 이게 없으면 UpdateDeviceInfo가 절대 안 불립니다.
            // Dock header
            Title = "Manual Focuser";

            // Some asynchronous initialization
            Task.Run(() => {
                //NighttimeData = nighttimeCalculator.Calculate();
                //nighttimeCalculator.OnReferenceDayChanged += NighttimeCalculator_OnReferenceDayChanged;
            });

            // Registering to profile service events to react on
            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };

            StopFocusControl = new RelayCommand(() => {
                FocusControlToken?.Cancel();
            });

            MoveToPosition = new RelayCommand(async () => {
                Moving = true;
                await focuser.MoveFocuser(TargetPos, FocusControlToken.Token);
                Moving = false;
            });

            MoveIN = new RelayCommand(async () => {
                Moving = true;
                await focuser.MoveFocuserRelative(-Math.Abs(UserStep), FocusControlToken.Token);
                Moving = false;
            });

            MoveOUT = new RelayCommand(async () => {
                Moving = true;
                await focuser.MoveFocuserRelative(+-Math.Abs(UserStep), FocusControlToken.Token);
                Moving = false;
            });

        }
    }
}
