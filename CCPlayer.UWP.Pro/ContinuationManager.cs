using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.Managers
{
    /// <summary>
    /// ContinuationManager is used to detect if the most recent activation was due
    /// to a continuation such as the FileOpenPicker or WebAuthenticationBroker
    /// </summary>
    public class ContinuationManager
    {
        public const string SOURCE_VIEW_MODEL_TYPE_FULL_NAME = "SourceViewModel";

        IContinuationActivatedEventArgs args = null;
        Guid id = Guid.Empty;

        private FrameworkElement GetCurrentView()
        {
            var frame = Window.Current.Content as Frame;
            if (frame != null)
                return frame.Content as FrameworkElement;

            return Window.Current.Content as FrameworkElement;
        }

        /// <summary>
        /// Sets the ContinuationArgs for this instance. Using default Frame of current Window
        /// Should be called by the main activation handling code in App.xaml.cs
        /// </summary>
        /// <param name="args">The activation args</param>
        internal void Continue(IContinuationActivatedEventArgs args)
        {
            var view = this.GetCurrentView();
            if (view == null)
                return;

            object dataContext = view.DataContext;

            //파일열기전 viewModel을 지정한 경우 overriding
            if (args.ContinuationData.ContainsKey(SOURCE_VIEW_MODEL_TYPE_FULL_NAME))
            {
                var viewModelTypeName = args.ContinuationData[SOURCE_VIEW_MODEL_TYPE_FULL_NAME] as string;
                dataContext = ServiceLocator.Current.GetInstance(Type.GetType(viewModelTypeName));
            }

            this.Continue(args, dataContext);
        }

        /// <summary>
        /// Sets the ContinuationArgs for this instance. Should be called by the main activation
        /// handling code in App.xaml.cs
        /// </summary>
        /// <param name="args">The activation args</param>
        /// <param name="rootFrame">The frame control that contains the current page</param>
        internal void Continue(IContinuationActivatedEventArgs args, object dataContext)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (this.args != null)
                throw new InvalidOperationException("Can't set args more than once");

            this.args = args;
            this.id = Guid.NewGuid();

            if (dataContext == null)
                return;

            switch (args.Kind)
            {
                case ActivationKind.PickFileContinuation:
                    var fileOpenPickerViewModel = dataContext as IFileOpenPickerContinuable;
                    if (fileOpenPickerViewModel != null)
                        fileOpenPickerViewModel.ContinueFileOpenPicker(args as FileOpenPickerContinuationEventArgs);
                    break;

                case ActivationKind.PickSaveFileContinuation:
                    var fileSavePickerViewModel = dataContext as IFileSavePickerContinuable;
                    if (fileSavePickerViewModel != null)
                        fileSavePickerViewModel.ContinueFileSavePicker(args as FileSavePickerContinuationEventArgs);
                    break;

                case ActivationKind.PickFolderContinuation:
                    var folderPickerViewModel = dataContext as IFolderPickerContinuable;
                    if (folderPickerViewModel != null)
                        folderPickerViewModel.ContinueFolderPicker(args as FolderPickerContinuationEventArgs);
                    break;

                case ActivationKind.WebAuthenticationBrokerContinuation:
                    var wabViewModel = dataContext as IWebAuthenticationContinuable;
                    if (wabViewModel != null)
                        wabViewModel.ContinueWebAuthentication(args as WebAuthenticationBrokerContinuationEventArgs);
                    break;
            }
        }

        /// <summary>
        /// Retrieves the continuation args, if they have not already been retrieved, and 
        /// prevents further retrieval via this property (to avoid accidentla double-usage)
        /// </summary>
        public IContinuationActivatedEventArgs ContinuationArgs
        {
            get
            {
                return args;
            }
        }

        /// <summary>
        /// Unique identifier for this particular continuation. Most useful for components that 
        /// retrieve the continuation data via <see cref="GetContinuationArgs"/> and need
        /// to perform their own replay check
        /// </summary>
        public Guid Id { get { return id; } }

    }

    /// <summary>
    /// Implement this interface if your page invokes the file open picker
    /// API.
    /// </summary>
    interface IFileOpenPickerContinuable
    {
        /// <summary>
        /// This method is invoked when the file open picker returns picked
        /// files
        /// </summary>
        /// <param name="args">Activated event args object that contains returned files from file open picker</param>
        void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args);
    }

    /// <summary>
    /// Implement this interface if your page invokes the file save picker
    /// API
    /// </summary>
    interface IFileSavePickerContinuable
    {
        /// <summary>
        /// This method is invoked when the file save picker returns saved
        /// files
        /// </summary>
        /// <param name="args">Activated event args object that contains returned file from file save picker</param>
        void ContinueFileSavePicker(FileSavePickerContinuationEventArgs args);
    }

    /// <summary>
    /// Implement this interface if your page invokes the folder picker API
    /// </summary>
    interface IFolderPickerContinuable
    {
        /// <summary>
        /// This method is invoked when the folder picker returns the picked
        /// folder
        /// </summary>
        /// <param name="args">Activated event args object that contains returned folder from folder picker</param>
        void ContinueFolderPicker(FolderPickerContinuationEventArgs args);
    }

    /// <summary>
    /// Implement this interface if your page invokes the web authentication
    /// broker
    /// </summary>
    interface IWebAuthenticationContinuable
    {
        /// <summary>
        /// This method is invoked when the web authentication broker returns
        /// with the authentication result
        /// </summary>
        /// <param name="args">Activated event args object that contains returned authentication token</param>
        void ContinueWebAuthentication(WebAuthenticationBrokerContinuationEventArgs args);
    }
}
