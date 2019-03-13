var aHandBreakPresetsGroups = [
'General', [0,16],
'Web', [16, 19],
'Devices', [19, 45],
'Matroska', [45, 61],
'Legacy', [61, 72]
];

var aHandBreakPresets = [
//General
'Very Fast 1080p30',
'Very Fast 720p30',
'Very Fast 576p25',
'Very Fast 480p30',
'Fast 1080p30',
'Fast 720p30',
'Fast 576p25',
'Fast 480p30',
'HQ 1080p30 Surround',
'HQ 720p30 Surround',
'HQ 576p25 Surround',
'HQ 480p30 Surround',
'Super HQ 1080p30 Surround',
'Super HQ 720p30 Surround',
'Super HQ 576p25 Surround',
'Super HQ 480p30 Surround',
//Web
'Gmail Large 3 Minutes 720p30',
'Gmail Medium 5 Minutes 480p30',
'Gmail Small 10 Minutes 288p30',
//Devices
'Android 1080p30',
'Android 720p30',
'Android 576p25',
'Android 480p30',
'Apple 1080p60 Surround',
'Apple 1080p30 Surround',
'Apple 720p30 Surround',
'Apple 540p30 Surround',
'Apple 240p30',
'Chromecast 1080p30 Surround',
'Fire TV 1080p30 Surround',
'Playstation 1080p30 Surround',
'Playstation 720p30',
'Playstation 540p30',
'Roku 2160p60 4K Surround',
'Roku 2160p30 4K Surround',
'Roku 1080p30 Surround',
'Roku 720p30 Surround',
'Roku 576p25',
'Roku 480p30',
'Windows Mobile 1080p30',
'Windows Mobile 720p30',
'Windows Mobile 540p30',
'Windows Mobile 480p30',
'Xbox 1080p30 Surround',
'Xbox Legacy 1080p30 Surround',
//Matroska
'H.264 MKV 1080p30',
'H.264 MKV 720p30',
'H.264 MKV 576p25',
'H.264 MKV 480p30',
'H.265 MKV 1080p30',
'H.265 MKV 720p30',
'H.265 MKV 576p25',
'H.265 MKV 480p30',
'VP8 MKV 1080p30',
'VP8 MKV 720p30',
'VP8 MKV 576p25',
'VP8 MKV 480p30',
'VP9 MKV 1080p30',
'VP9 MKV 720p30',
'VP9 MKV 576p25',
'VP9 MKV 480p30',
//Legacy
'Normal',
'High Profile',
'Universal',
'iPod',
'iPhone & iPod touch',
'iPad',
'AppleTV',
'AppleTV 2',
'AppleTV 3',
'Android',
'Android Tablet',
'Windows Phone 8'
];

var aHandBreakPresetsDescription = [
//General
'Small H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container.',
'Small H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container.',
'Small H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container.',
'Small H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container.',
'H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container.',
'H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container.',
'H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container.',
'H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container.',
'High quality H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'High quality H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'High quality H.264 video (up to 576p25), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'High quality H.264 video (up to 480p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'Super high quality H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'Super high quality H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'Super high quality H.264 video (up to 576p25), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
'Super high quality H.264 video (up to 480p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container.',
//Web
'Encode up to 3 minutes of video in large size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container.',
'Encode up to 5 minutes of video in medium size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container.',
'Encode up to 10 minutes of video in small size for Gmail (25 MB or less). H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container.',
//Devices
'H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices.',
'H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices.',
'H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container. Compatible with Android devices.',
'H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Android devices.',
'H.264 video (up to 1080p60), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPad Pro; iPad Air; iPad mini 2nd, 3rd Generation and later; Apple TV 4th Generation and later.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 5, 5S, 6, 6s, and later; iPod touch 6th Generation and later; iPad Pro; iPad Air; iPad 3rd, 4th Generation and later; iPad mini; Apple TV 3rd Generation and later.',
'H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 4 and later; iPod touch 4th, 5th Generation and later; Apple TV 2nd Generation and later.',
'H.264 video (up to 540p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Apple iPhone 1st Generation, 3G, 3GS, and later; iPod touch 1st, 2nd, 3rd Generation and later; iPod Classic; Apple TV 1st Generation and later.',
'H.264 video (up to 240p30) and AAC stereo audio, in an MP4 container. Compatible with Apple iPod 5th Generation and later.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Google Chromecast.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) audio, in an MP4 container. Compatible with Amazon Fire TV.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Playstation 3 and 4.',
'H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Playstation Vita TV.',
'H.264 video (up to 540p30) and AAC stereo audio, in an MP4 container. Compatible with Playstation Vita.',
'H.265 video (up to 2160p60), AAC stereo audio, and surround audio, in an MKV container. Compatible with Roku 4.',
'H.265 video (up to 2160p30), AAC stereo audio, and surround audio, in an MKV container. Compatible with Roku 4.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Roku 1080p models.',
'H.264 video (up to 720p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Roku 720p models.',
'H.264 video (up to 576p25) and AAC stereo audio, in an MP4 container. Compatible with Roku standard definition models.',
'H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Roku standard definition models.',
'H.264 video (up to 1080p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon 800 (MSM8974), S4 (MSM8x30, MSM8960), and better CPUs.',
'H.264 video (up to 720p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon S4 (MSM8x27), S2 (MSM8x55), S1 (MSM8x50), and better CPUs.',
'H.264 video (up to 540p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon 200 (MSM8210, MSM8212) and better CPUs.',
'H.264 video (up to 480p30) and AAC stereo audio, in an MP4 container. Compatible with Windows Mobile devices with Qualcomm Snapdragon S1 (MSM7x27a) and better CPUs.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Xbox One.',
'H.264 video (up to 1080p30), AAC stereo audio, and Dolby Digital (AC-3) surround audio, in an MP4 container. Compatible with Xbox 360.',
//Matroska
'H.264 video (up to 1080p30) and AAC stereo audio, in an MKV container.',
'H.264 video (up to 720p30) and AAC stereo audio, in an MKV container.',
'H.264 video (up to 576p25) and AAC stereo audio, in an MKV container.',
'H.264 video (up to 480p30) and AAC stereo audio, in an MKV container.',
'H.265 video (up to 1080p30) and AAC stereo audio, in an MKV container.',
'H.265 video (up to 720p30) and AAC stereo audio, in an MKV container.',
'H.265 video (up to 576p25) and AAC stereo audio, in an MKV container.',
'H.265 video (up to 480p30) and AAC stereo audio, in an MKV container.',
'VP8 video (up to 1080p30) and Vorbis stereo audio, in an MKV container.',
'VP8 video (up to 720p30) and Vorbis stereo audio, in an MKV container.',
'VP8 video (up to 576p25) and Vorbis stereo audio, in an MKV container.',
'VP8 video (up to 480p30) and Vorbis stereo audio, in an MKV container.',
'VP9 video (up to 1080p30) and Opus stereo audio, in an MKV container.',
'VP9 video (up to 720p30) and Opus stereo audio, in an MKV container.',
'VP9 video (up to 576p25) and Opus stereo audio, in an MKV container.',
'VP9 video (up to 480p30) and Opus stereo audio, in an MKV container.',
//Legacy
'Legacy HandBrake 0.10.x H.264 Main Profile preset.',
'Legacy HandBrake 0.10.x H.264 High Profile preset.',
'Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound and compatible with nearly all Apple devices.',
'Legacy HandBrake 0.10.x preset compatible with Apple iPod 5th Generation and later.',
'Legacy HandBrake 0.10.x preset compatible with Apple iPhone 4, iPod touch 3rd Generation, and later devices.',
'Legacy HandBrake 0.10.x preset compatible with Apple iPad (all generations).',
'Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 1st Generation and later.',
'Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 2nd Generation and later.',
'Legacy HandBrake 0.10.x preset including Dolby Digital (AC-3) surround sound, compatible with Apple TV 3rd Generation and later.',
'Legacy HandBrake 0.10.x preset compatible with Android 2.3 and later handheld devices.',
'Legacy HandBrake 0.10.x preset compatible with Android 2.3 and later tablets.',
'Legacy HandBrake 0.10.x preset compatible with most Windows Phone 8 devices.'
];

function listPresetsAsGroups()
{
	var sOutput = "";
	var i,o;
	for(i=0;i<aHandBreakPresetsGroups.length;i=i+2)
	{
		//console.log(aHandBreakPresetsGroups[i]);
		sOutput += '<optgroup label="' + aHandBreakPresetsGroups[i] + '">';
		for(o=aHandBreakPresetsGroups[i+1][0];o<aHandBreakPresetsGroups[i+1][1];o++)
			//console.log('[' + o +'] ---> ' + aHandBreakPresets[o]);
			sOutput += '<option value="' + o + '">' + aHandBreakPresets[o] + '</option>';
		sOutput += '</optgroup>';
	}
	
	return sOutput;
}

function listPresets()
{
	var i,o;
	for(i=0;i<aHandBreakPresetsGroups.length;i=i+2)
	{
		console.log(aHandBreakPresetsGroups[i]);
		for(o=aHandBreakPresetsGroups[i+1][0];o<aHandBreakPresetsGroups[i+1][1];o++)
		{
			console.log('[' + o +'] ---> ' + aHandBreakPresets[o]);
			console.log('-----------> ' + aHandBreakPresetsDescription[o]);
		}
	}
}