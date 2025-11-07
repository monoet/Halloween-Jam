namespace BattleV2.AnimationSystem
{
    public readonly struct RouterDiagnosticsInfo
    {
        public static readonly RouterDiagnosticsInfo Empty = new RouterDiagnosticsInfo(false, false, false, false, null, null, null, null);

        public RouterDiagnosticsInfo(
            bool hasVfxService,
            bool hasSfxService,
            bool hasCameraService,
            bool hasUiService,
            string vfxServiceType,
            string sfxServiceType,
            string cameraServiceType,
            string uiServiceType)
        {
            HasVfxService = hasVfxService;
            HasSfxService = hasSfxService;
            HasCameraService = hasCameraService;
            HasUiService = hasUiService;
            VfxServiceType = vfxServiceType ?? "(null)";
            SfxServiceType = sfxServiceType ?? "(null)";
            CameraServiceType = cameraServiceType ?? "(null)";
            UiServiceType = uiServiceType ?? "(null)";
        }

        public bool HasVfxService { get; }
        public bool HasSfxService { get; }
        public bool HasCameraService { get; }
        public bool HasUiService { get; }
        public string VfxServiceType { get; }
        public string SfxServiceType { get; }
        public string CameraServiceType { get; }
        public string UiServiceType { get; }
    }
}
