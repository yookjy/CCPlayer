#pragma once

extern "C"
{
#include <libavcodec\avcodec.h>
}

//#if _FF30
#if _M_ARM
#define GET_CODEC_CTX_PARAM(STREAM, VALUE) STREAM##->codec->##VALUE 
#define FILL_CODEC_CTX(CODEC_CTX, STREAM) avcodec_copy_context(CODEC_CTX, STREAM##->codec)

inline int decode_frame(AVCodecContext *avctx, AVFrame *picture,
	int *got_picture_ptr, AVPacket *avpkt, bool isVideo)
{
	int ret = 0;
	if (isVideo)
		ret = avcodec_decode_video2(avctx, picture, got_picture_ptr, avpkt);
	else
		ret = avcodec_decode_audio4(avctx, picture, got_picture_ptr, avpkt);

	if (ret >= 0)
		avpkt->size = 0;

	return ret;
}

#else 
#define GET_CODEC_CTX_PARAM(STREAM, VALUE) STREAM##->codecpar->##VALUE 
#define FILL_CODEC_CTX(CODEC_CTX, STREAM) avcodec_parameters_to_context(CODEC_CTX, STREAM##->codecpar)

inline int decode_frame(AVCodecContext *avctx, AVFrame *picture,
	int *got_picture_ptr, AVPacket *avpkt, bool isVideo)
{
	int ret = avcodec_send_packet(avctx, avpkt);
	if (ret < 0 && ret != AVERROR(EAGAIN) && ret != AVERROR_EOF)
		return ret;
	if (ret >= 0)
		avpkt->size = 0;
	ret = avcodec_receive_frame(avctx, picture);
	if (ret >= 0)
		*got_picture_ptr = 1;
	if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
		ret = 0;

	return ret;
}
#endif
