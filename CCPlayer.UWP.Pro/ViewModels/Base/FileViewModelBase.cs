using CCPlayer.UWP.Models;
using CCPlayer.UWP.Xaml.Controls;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Globalization.Collation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.ViewModels.Base
{
    public abstract class FileViewModelBase : CCPThumbnailViewModelBase
    {
        protected List<Models.Thumbnail> _ThumbnailListInCurrentFolder;

        protected List<string> _CurrentSubtitleFileList;

        protected CharacterGroupings _CharacterGroupings;

        [DoNotNotify]
        public ResourceDictionary Resources { get; set; }

        [DoNotNotify]
        public ObservableCollection<KeyName> OrderBySource { get; set; }

        public string Title { get; set; }

        public bool VisibleToUpper { get; set; }

        private bool _IsLoadingFolders;
        [DoNotNotify]
        protected bool IsLoadingFolders
        {
            set { _IsLoadingFolders = value; IsStopLoadingIndicator = (!_IsLoadingFolders && !_IsLoadingFiles); }
        }

        private bool _IsLoadingFiles;
        [DoNotNotify]
        protected bool IsLoadingFiles
        {
            set { _IsLoadingFiles = value; IsStopLoadingIndicator = (!_IsLoadingFolders && !_IsLoadingFiles); }
        }

        protected bool _IsStopLoadingIndicator;
        [DoNotNotify]
        public virtual bool IsStopLoadingIndicator
        {
            get { return _IsStopLoadingIndicator; }
            set { Set(ref _IsStopLoadingIndicator, value); }
        }

        public bool ShowOrderBy { get; set; }

        public bool? IsMediaInformationMode { get; set; } = false;

        public StyleSelector VideoItemStyleSelector { get; set; }

        public bool ShowErrorMessage { get; set; }

        public string ErrorMessage { get; set; }

        public string DisplayCurrentPath { get; set; }

        protected SortType _Sort;
        private string _OrderBy;
        [DoNotNotify]
        public string OrderBy
        {
            get { return _OrderBy; }
            set
            {
                //enum 변환
                Enum.TryParse<SortType>(value, out _Sort);
                if (Set(ref _OrderBy, value))
                {
                    SaveOrderBySetting();
                    OrderByChanged();
                }
            }
        }

        protected void CreateOrderBySource()
        {
            var resource = ResourceLoader.GetForCurrentView();

            OrderBySource.Add(new KeyName(SortType.Name.ToString(), resource.GetString("Sort/Name/Ascending")));
            OrderBySource.Add(new KeyName(SortType.NameDescending.ToString(), resource.GetString("Sort/Name/Descending")));
            OrderBySource.Add(new KeyName(SortType.CreatedDate.ToString(), resource.GetString("Sort/CreatedDate/Ascending")));
            OrderBySource.Add(new KeyName(SortType.CreatedDateDescending.ToString(), resource.GetString("Sort/CreatedDate/Descending")));
        }

        protected void CreateCharacterGroupings()
        {
            // Get the letters representing each group for current language using CharacterGroupings class
            _CharacterGroupings = new CharacterGroupings();
            // Create dictionary for the letters and replace '...' with proper globe icon
            var keys = _CharacterGroupings.Where(x => x.Label.Count() >= 1)
                .Select(x => x.Label)
                .ToDictionary(x => x);
            keys["..."] = "\uD83C\uDF10";
        }

        protected void ChangeStyleSelector(string styleSelectorName, double width)
        {
            string postfix = "320";
            if (width >= 720)
                postfix = "720";
            else if (width >= 432)
                postfix = "432";
            else if (width >= 411)
                postfix = "411";
            else if (width >= 360)
                postfix = "360";
            else if (width >= 341)
                postfix = "341";
            else if (width >= 0)
                postfix = "320";

            string key = styleSelectorName + postfix;
            if (this.Resources != null && this.Resources.ContainsKey(key))
            {
                var newStyleSelector = this.Resources[key] as StyleSelector;
                if (VideoItemStyleSelector != newStyleSelector)
                {
                    VideoItemStyleSelector = newStyleSelector;
                }
            }
        }

        abstract protected void OrderByChanged();

        abstract protected void SaveOrderBySetting();
    }
}
