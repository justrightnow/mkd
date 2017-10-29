Windows app that combines different APIs (<a href="http://www.xfyun.cn/services/voicedictation">iFlytek-科大讯飞</a>, <a href="https://traslate.google.cn">Google Translate</a>, <a href="https://azure.microsoft.com/en-us/services/cognitive-services/speech/">Bing Speech</a>, <a href="http://fanyi-api.baidu.com/api/trans/product/index">Baidu Translate</a>) to get the best translated caption/subtitles for China market:

<p align="center"><img src="https://user-images.githubusercontent.com/24521991/32063973-2f13fd20-baab-11e7-93c1-61155a152a3c.png" width="500"></p>

<p align="center"><b>Except Google Translate, for each other service you must get an ID</b></p>
<br/>

Screenshot of the program:
<p align="center"><img src="https://user-images.githubusercontent.com/24521991/32085308-d218c690-bb00-11e7-86d1-debebfe03c76.jpg"></p>
Comments:
<ul>
<li>No matter what is on the screen (powerpoint, video, etc.) the subtitles will always be on top of it</li>
<li>You can also run <b>audio files</b> (currently only from English to Chinese)</li>
<li>The transcript of the recognized audio and translation will be stored in a .txt file once you select the "副本" box</li>
<li>You can record speaker voice by selecting "录音" box</li>
</ul>
<br/>
<hr></hr>
<br/>
<p>1. From <b>Chinese audio to English caption/subtitles</b>, the program uses:</p>

<u>
<li>Chinese voice recognition: <a href="http://www.xfyun.cn/services/voicedictation">iFlytek-科大讯飞</a></li>
<li>Chinese to English text translation: <a href="https://traslate.google.cn">Google Translate</a></li>
</u>

<br/>
<br/>

<p align="center"><img src="https://user-images.githubusercontent.com/24521991/32063586-2729e396-baaa-11e7-9f0d-71f921fba63f.png" width="500"></p>
<br/>

<p>2. From <b>English audio to Chinese caption/subtitles</b>, the program uses:</p>

<u>
<li>English voice recognition: <a href="https://azure.microsoft.com/en-us/services/cognitive-services/speech/">Bing Speech</a></li>
<li>English to Chinese translation: <a href="http://fanyi-api.baidu.com/api/trans/product/index">Baidu Translate</a></li>
</u>

<br/>
<p align="center"><img src="https://user-images.githubusercontent.com/24521991/32063559-108eba8a-baaa-11e7-93b2-f4baecc82aff.png" width="500"></p>

<br/>
<hr></hr>

<h1>iFlytek</h1>
<a href="http://www.xfyun.cn/services/voicedictation">iFlytek-科大讯飞</a>
<br/>
<p>In order to make the Chinese voice recognition work you still need to follow next 2 steps:</p>
<ul>
<li>Once you get your ID, download the SDK from your portal (below pic) 
<li>Copy/paste the msc.dll file in your bin/debug folder. This file is unique for each ID.
</ul>
<p align="center"><img src="https://user-images.githubusercontent.com/24521991/32142652-83a1f13a-bcd6-11e7-9898-8535c88a85cc.png"></p>

<br/>
Open to any suggestion! Thanks!!
