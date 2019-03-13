using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOTRd
{
    public class MOTR_Convertprofiles
    {
        #region ConvertHeader
        internal struct ConvertHeader
        {
            public string sHeader;
            public int nFrom;
            public int nTo;

            public ConvertHeader(string sHeader, int nFrom, int nTo)
            {
                this.sHeader = sHeader;
                this.nFrom = nFrom;
                this.nTo = nTo;
            }
        }
        class ConvertHeaders : List<ConvertHeader>
        {
            public void Add(string sHeader, int nFrom, int nTo)
            {
                Add(new ConvertHeader(sHeader, nFrom, nTo));
            }
        }
        #endregion
        #region ConvertItem
        internal struct ConvertItem
        {
            public string sProfile;
            public string sDescription;

            public ConvertItem(string sProfile, string sDescription)
            {
                this.sProfile = sProfile;
                this.sDescription = sDescription;
            }
        }
        class ConvertItems : List<ConvertItem>
        {
            public void Add(string sProfile, string sDescription)
            {
                Add(new ConvertItem(sProfile, sDescription));
            }
        }
        #endregion

        //Variables that holds data
        private ConvertHeaders convertHeaders;
        private ConvertItems convertItems;

        public  MOTR_Convertprofiles()
        {
            //Add the headers we have
            convertHeaders = new ConvertHeaders
            {
                {"General", 0, 15 },
                {"Web", 16, 18 },
                {"Devices", 19, 44 },
                {"Matroska", 45, 60 },
                {"Legacy", 61, 71 }
            };

            //Add the profiles
            #region ConvertItemsArray
            convertItems = new ConvertItems
            {
                //General
                {"Very Fast 1080p30", "Small H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container." },
                {"Very Fast 720p30", "Small H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container." },
                {"Very Fast 576p25", "Small H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container." },
                {"Very Fast 480p30", "Small H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container." },
                {"Fast 1080p30", "H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container." },
                {"Fast 720p30", "H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container." },
                {"Fast 576p25", "H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container." },
                {"Fast 480p30", "H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container." },
                {"HQ 1080p30 Surround", "High quality H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"HQ 720p30 Surround", "High quality H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"HQ 576p25 Surround", "High quality H.264 video (up to 576p25), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"HQ 480p30 Surround", "High quality H.264 video (up to 480p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"Super HQ 1080p30 Surround", "Super high quality H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"Super HQ 720p30 Surround", "Super high quality H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"Super HQ 576p25 Surround", "Super high quality H.264 video (up to 576p25), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                {"Super HQ 480p30 Surround", "Super high quality H.264 video (up to 480p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container." },
                //Web
                {"Gmail Large 3 Minutes 720p30", "Encode up to 3 minutes of video in large size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container." },
                {"Gmail Medium 5 Minutes 480p30", "Encode up to 5 minutes of video in medium size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container." },
                {"Gmail Small 10 Minutes 288p30", "Encode up to 10 minutes of video in small size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container." },
                //Devices
                {"Android 1080p30", "H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices." },
                {"Android 720p30", "H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices." },
                {"Android 576p25", "H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container. Compatible with Android devices." },
                {"Android 480p30", "H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices." },
                {"Apple 1080p60 Surround", "H.264 video (up to 1080p60), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPad Pro; iPad Air; iPad mini 2nd, 3rd Generation and later; Apple TV 4th Generation and later." },
                {"Apple 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 5, 5S, 6, 6s, and later; iPod touch 6th Generation and later; iPad Pro; iPad Air; iPad 3rd, 4th Generation and later; iPad mini; Apple TV 3rd Generation and later." },
                {"Apple 720p30 Surround", "H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 4 and later; iPod touch 4th, 5th Generation and later; Apple TV 2nd Generation and later." },
                {"Apple 540p30 Surround", "H.264 video (up to 540p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 1st Generation, 3G, 3GS, and later; iPod touch 1st, 2nd, 3rd Generation and later; iPod Classic; Apple TV 1st Generation and later." },
                {"Apple 240p30", "H.264 video (up to 240p30) and AAC stereo audio, in an MP4 container. Compatible with Apple iPod 5th Generation and later." },
                {"Chromecast 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Google Chromecast." },
                {"Fire TV 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) audio, in an MP4 container. Compatible with Amazon Fire TV." },
                {"Playstation 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Playstation 3 and 4." },
                {"Playstation 720p30", "H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Playstation Vita TV." },
                {"Playstation 540p30", "H.264 video (up to 540p30) and AAC stereo audio, in an MP4 container. Compatible with Playstation Vita." },
                {"Roku 2160p60 4K Surround", "H.265 video (up to 2160p60), AAC stereo audio, and surround audio, in an MKV container. Compatible with Roku 4." },
                {"Roku 2160p30 4K Surround", "H.265 video (up to 2160p30), AAC stereo audio, and surround audio, in an MKV container. Compatible with Roku 4." },
                {"Roku 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Roku 1080p models." },
                {"Roku 720p30 Surround", "H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Roku 720p models." },
                {"Roku 576p25", "H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container. Compatible with Roku standard definition models." },
                {"Roku 480p30", "H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Roku standard definition models." },
                {"Windows Mobile 1080p30", "H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon 800 (MSM8974), S4 (MSM8x30, MSM8960), and better CPUs." },
                {"Windows Mobile 720p30", "H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon S4 (MSM8x27), S2 (MSM8x55), S1 (MSM8x50), and better CPUs." },
                {"Windows Mobile 540p30", "H.264 video (up to 540p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon 200 (MSM8210, MSM8212) and better CPUs." },
                {"Windows Mobile 480p30", "H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon S1 (MSM7x27a) and better CPUs." },
                {"Xbox 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Xbox One." },
                {"Xbox Legacy 1080p30 Surround", "H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Xbox 360." },
                //Matroska
                {"H.264 MKV 1080p30", "H.264 video (up to 1080p30) and AAC stereo audio, in an MKV container." },
                {"H.264 MKV 720p30", "H.264 video (up to 720p30) and AAC stereo audio, in an MKV container." },
                {"H.264 MKV 576p25", "H.264 video (up to 576p25) and AAC stereo audio, in an MKV container." },
                {"H.264 MKV 480p30", "H.264 video (up to 480p30) and AAC stereo audio, in an MKV container." },
                {"H.265 MKV 1080p30", "H.265 video (up to 1080p30) and AAC stereo audio, in an MKV container." },
                {"H.265 MKV 720p30", "H.265 video (up to 720p30) and AAC stereo audio, in an MKV container." },
                {"H.265 MKV 576p25", "H.265 video (up to 576p25) and AAC stereo audio, in an MKV container." },
                {"H.265 MKV 480p30", "H.265 video (up to 480p30) and AAC stereo audio, in an MKV container." },
                {"VP8 MKV 1080p30", "VP8 video (up to 1080p30) and Vorbis stereo audio, in an MKV container." },
                {"VP8 MKV 720p30", "VP8 video (up to 720p30) and Vorbis stereo audio, in an MKV container." },
                {"VP8 MKV 576p25", "VP8 video (up to 576p25) and Vorbis stereo audio, in an MKV container." },
                {"VP8 MKV 480p30", "VP8 video (up to 480p30) and Vorbis stereo audio, in an MKV container." },
                {"VP9 MKV 1080p30", "VP9 video (up to 1080p30) and Opus stereo audio, in an MKV container." },
                {"VP9 MKV 720p30", "VP9 video (up to 720p30) and Opus stereo audio, in an MKV container." },
                {"VP9 MKV 576p25", "VP9 video (up to 576p25) and Opus stereo audio, in an MKV container." },
                {"VP9 MKV 480p30", "VP9 video (up to 480p30) and Opus stereo audio, in an MKV container." },
                //Legacy
                {"Normal", "Legacy HandBrake 0.10.x H.264 Main Profile preset." },
                {"High Profile", "Legacy HandBrake 0.10.x H.264 High Profile preset." },
                {"Universal", "Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound and compatible with nearly all Apple devices." },
                {"iPod", "Legacy HandBrake 0.10.x preset compatible with Apple iPod 5th Generation and later." },
                {"iPhone & iPod touch", "Legacy HandBrake 0.10.x preset compatible with Apple iPhone 4, iPod touch 3rd Generation, and later devices." },
                {"iPad", "Legacy HandBrake 0.10.x preset compatible with Apple iPad (all generations)." },
                {"AppleTV", "Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 1st Generation and later." },
                {"AppleTV 2", "Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 2nd Generation and later." },
                {"AppleTV 3", "Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 3rd Generation and later." },
                {"Android", "Legacy HandBrake 0.10.x preset compatible with Android 2.3 and later handheld devices." },
                {"Android Tablet", "Legacy HandBrake 0.10.x preset compatible with Android 2.3 and later tablets." },
                {"Windows Phone 8", "Legacy HandBrake 0.10.x preset compatible with most Windows Phone 8 devices." }
            };
            #endregion
        }

        //Returns header;from;to
        public string GetHeaders()
        {
            string sReturn = "";
            for (int i = 0; i < convertHeaders.Count; i++)
                sReturn += convertHeaders[i].sHeader + ";" + convertHeaders[i].nFrom.ToString() + ";" + convertHeaders[i].nTo.ToString();
            return sReturn;
        }
        public ArrayList GetHeadersArray()
        {
            ArrayList arrayList = new ArrayList(convertHeaders);
            return arrayList;
        }

        //Returns profiles
        public string GetProfiles()
        {
            string sReturn = "";
            for (int i = 0; i < convertItems.Count; i++)
                sReturn += convertItems[i].sProfile + ";";

            return sReturn;
        }
        public ArrayList GetProfilesArray()
        {
            //Return only the profilename
            ArrayList arrayList = new ArrayList(convertItems
            .Select(x => { return x.sProfile; })
            .ToList());
            return arrayList;
        }

        //Returns the description of one item
        public string GetDescription(int nProfile)
        {
            if (nProfile < 0 || nProfile > convertItems.Count)
                return "<invalid profile number>";
            return convertItems[nProfile].sDescription;
        }
        public string GetDescription(string sProfile)
        {
            for (int i = 0; i < convertItems.Count; i++)
                if (convertItems[i].sProfile.ToUpper() == sProfile.ToUpper())
                    return GetDescription(i);
            return "<invalid profile name>";
        }

    }
}
