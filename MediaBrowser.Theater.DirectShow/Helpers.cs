﻿using MediaBrowser.Theater.Interfaces.Configuration;
using System;
using System.Management;
using System.Collections.Generic;
using CoreAudioApi;

namespace MediaBrowser.Theater.DirectShow
{
    public static class VideoConfigurationUtils
    {
        //we only need to do this once per run
        private static string _gpuModel = String.Empty;

        public static string GpuModel
        {
            get
            {
                if (String.IsNullOrWhiteSpace(_gpuModel))
                {
                    //this may not work for multi-GPU systems
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DisplayConfiguration"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            foreach (PropertyData property in mo.Properties)
                            {
                                if (property.Name == "Description")
                                {
                                    _gpuModel = property.Value.ToString();
                                    break;
                                }
                            }
                            mo.Dispose();
                        }
                    }
                }
                return _gpuModel;
            }
        }

        public static LAVHWAccel GetHwaMode(VideoConfiguration config, bool preferDXVA2)
        {
            if (config.HwaMode > -1)
                return (LAVHWAccel)config.HwaMode;
            else if (preferDXVA2)
                return LAVHWAccel.DXVA2Native;
            else
            {
                if (GpuModel.IndexOf("Intel") > -1)
                    return LAVHWAccel.QuickSync;
                else
                    return LAVHWAccel.DXVA2CopyBack;
            }
        }

        public static int GetHwaResolutions(VideoConfiguration config)
        {
            if (config.HwaResolution > -1)
                return config.HwaResolution;

            if (GpuModel.IndexOf("Intel") > -1)
                return 7; // SD + HD + UHD
            else
                return 3; // SD + HD;
        }
    }

    public static class AudioConfigurationUtils
    {
        public static Dictionary<string, string> GetAudioDevices()
        {
            Dictionary<string, string> audioDevices = new Dictionary<string, string>();

            audioDevices["Default Device"] = string.Empty;

            MMDeviceEnumerator DevEnum = new MMDeviceEnumerator();
            MMDeviceCollection dc = DevEnum.EnumerateAudioEndPoints(EDataFlow.eRender, EDeviceState.DEVICE_STATE_ACTIVE);
            for (int i = 0; i < dc.Count; i++)
            {
                audioDevices[dc[i].FriendlyName] = dc[i].ID;
            }

            return audioDevices;
        }
    }
}
