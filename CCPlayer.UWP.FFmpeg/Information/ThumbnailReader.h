//*****************************************************************************
//
//	Copyright 2015 Microsoft Corporation
//
//	Licensed under the Apache License, Version 2.0 (the "License");
//	you may not use this file except in compliance with the License.
//	You may obtain a copy of the License at
//
//	http ://www.apache.org/licenses/LICENSE-2.0
//
//	Unless required by applicable law or agreed to in writing, software
//	distributed under the License is distributed on an "AS IS" BASIS,
//	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//	See the License for the specific language governing permissions and
//	limitations under the License.
//
//*****************************************************************************

#pragma once
#include <queue>

extern "C"
{
#include <libavformat/avformat.h>
#include <libswscale/swscale.h>
}

using namespace Windows::Storage::Streams;
using namespace Windows::Media::Core;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Information
			{
				ref class ThumbnailReader
				{
				public:
					virtual ~ThumbnailReader();
					Array<byte>^ GetBitmapData(Size size);
					
				private:
					int m_streamIndex;
					SwsContext* m_pSwsCtx;

				internal:
					ThumbnailReader(
						AVFormatContext* avFormatCtx,
						AVCodecContext* avCodecCtx,
						int streamIndex);
					// The FFmpeg context. Because they are complex types
					// we declare them as internal so they don't get exposed
					// externally
					AVFormatContext* m_pAvFormatCtx;
					AVCodecContext* m_pAvCodecCtx;
				};
			}
		}
	}
}