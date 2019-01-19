using CCPlayer.UWP.Common.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace CCPlayer.UWP.Common.Interface
{
    public interface IAttachmentDecoderConnector
    {
        event AttachmentPopulatedEventHandler AttachmentPopulatedEvent;

        event AttachmentCompletedEventHandler AttachmentCompletedEvent;

        bool IsSaveAttachment { get; set; }

        void SetUICoreDispatcher(CoreDispatcher dispatcher);

        void Populate(AttachmentData attachment);

        void Completed();
    }
}
