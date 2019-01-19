using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPlayer.UWP.Common.Codec;
using Windows.UI.Core;

namespace CCPlayer.UWP.Factory.Dummy.Connector
{
    public sealed class AttachmentDecoderConnector : IAttachmentDecoderConnector
    {
        private AttachmentDecoderConnector() { }

        public AttachmentDecoderConnector(CoreDispatcher dispatcher)
        {
            SetUICoreDispatcher(dispatcher);
        }

        private CoreDispatcher _UIDispatcher;

        public bool IsSaveAttachment { get; set; }

        public event AttachmentPopulatedEventHandler AttachmentPopulatedEvent;
        public event AttachmentCompletedEventHandler AttachmentCompletedEvent;
        public void SetUICoreDispatcher(CoreDispatcher dispatcher)
        {
            _UIDispatcher = dispatcher;
        }

        public async void Populate(AttachmentData attachment)
        {
            if (IsSaveAttachment)
            {
                await _UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    AttachmentPopulatedEvent?.Invoke(this, attachment);
                });
            }
        }
        
        public async void Completed()
        {
            if (IsSaveAttachment)
            {
                await _UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    AttachmentCompletedEvent?.Invoke(this, null);
                });
            }
        }
    }
}
