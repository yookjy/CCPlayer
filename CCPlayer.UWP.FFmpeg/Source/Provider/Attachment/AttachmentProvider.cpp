#include "pch.h"
#include "AttachmentProvider.h"
#include "Common\FFmpegMacro.h"

AttachmentProvider::AttachmentProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::SW)
{
}

AttachmentProvider::~AttachmentProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

HRESULT AttachmentProvider::CreateMediaType(IMFMediaType** mediaType)
{
	return E_NOTIMPL;
}

HRESULT AttachmentProvider::WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	return E_NOTIMPL;
}

void AttachmentProvider::PushPacket(AVPacket avPacket)
{
}

void AttachmentProvider::Flush()
{
	if (m_pAvCodecCtx != NULL)
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}

void AttachmentProvider::PopulateAttachment(int streamIndex)
{
	auto attachmentDecoderConnector = m_pReader->GetAttachmentDecoderConnector();
	if (attachmentDecoderConnector != nullptr)
	{
		AVStream* stream = m_pAvFormatCtx->streams[streamIndex];
		AVDictionary* dict = stream->metadata;
		AVDictionaryEntry *t = NULL;

		String^ fileName = nullptr;
		String^ mimeType = nullptr;

		while (t = av_dict_get(dict, "", t, AV_DICT_IGNORE_SUFFIX))
		{
			auto key = t->key;
			auto val = t->value;

			if (strcmp(key, "filename") == 0)
			{
				fileName = ToStringHat(val);
			}
			else if (strcmp(key, "mimetype") == 0)
			{
				mimeType = ToStringHat(val);
			}
		}

		auto attachment = ref new AttachmentData();
		attachment->FileName = fileName;
		attachment->MimeType = mimeType;
		attachment->BinaryData = ref new Platform::Array<uint8_t>(GET_CODEC_CTX_PARAM(stream, extradata), GET_CODEC_CTX_PARAM(stream, extradata_size));

		attachmentDecoderConnector->Populate(attachment);
	}
}