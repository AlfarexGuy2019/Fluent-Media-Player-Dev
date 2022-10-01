﻿using CommunityToolkit.Mvvm.Input;
using Rise.Common.Enums;
using Rise.Common.Extensions;
using Rise.Data.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace Rise.Data.Sources
{
    public partial class NavViewDataSource
    {
        #region Private fields
        private const string _fileName = "ItemData.json";
        private const string _tmpFileName = "ItemData.json.~tmp";

        private readonly Dictionary<string, string> _defaultIcons =
            new Dictionary<string, string>();
        #endregion

        #region Public properties
        /// <summary>
        /// Contains the NavView items.
        /// </summary>
        public ObservableCollection<NavViewItemViewModel> Items { get; } =
            new ObservableCollection<NavViewItemViewModel>();

        /// <summary>
        /// Contains the footer NavView items.
        /// </summary>
        public ObservableCollection<NavViewItemViewModel> FooterItems { get; } =
            new ObservableCollection<NavViewItemViewModel>();
        #endregion

        #region Constructor
        public NavViewDataSource()
        {
            // Populate the default icon dictionary.
            _defaultIcons.Add("HomePage", "\uECA5");
            _defaultIcons.Add("PlaylistsPage", "\uE8FD");
            _defaultIcons.Add("SongsPage", "\uEC4F");
            _defaultIcons.Add("ArtistsPage", "\uE125");
            _defaultIcons.Add("AlbumsPage", "\uE93C");
            _defaultIcons.Add("GenresPage", "\uE138");
            _defaultIcons.Add("LocalVideosPage", "\uE8B2");
            _defaultIcons.Add("VideoPlaybackPage", "\uE1D9");
            _defaultIcons.Add("DiscyPage", "\uE9CE");
            _defaultIcons.Add("SettingsPage", "\uE115");
        }
        #endregion

        #region Data
        /// <summary>
        /// Populates <see cref="Items"/> and <see cref="FooterItems"/>
        /// with data from a JSON file.
        /// </summary>
        public async Task PopulateGroupsAsync()
        {
            // No need to populate groups more than once
            if (Items.Count != 0 || FooterItems.Count != 0)
            {
                return;
            }

            StorageFile file;
            string path = Path.Combine(ApplicationData.
                Current.LocalFolder.Path, _fileName);

            // If the file doesn't exist, get data from the placeholder
            if (File.Exists(path))
            {
                file = await ApplicationData.Current.LocalFolder.
                    GetFileAsync(_fileName);
            }
            else
            {
                var dataUri = new Uri($"ms-appx:///Assets/{_fileName}");
                file = await StorageFile.GetFileFromApplicationUriAsync(dataUri);
            }

            string jsonText = await FileIO.ReadTextAsync(file);

            // So, why check for this? The file should have the info, right?
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                // For some unexplainable reason, Windows will sometimes save
                // stuff to a tmp file and call it a day. We have to account
                // for that. I'm not joking when I say not checking for this
                // can crash the app, every single time it starts up.
                file = await ApplicationData.Current.LocalFolder.
                    GetFileAsync(_tmpFileName);

                jsonText = await FileIO.ReadTextAsync(file);
            }

            var obj = JsonObject.Parse(jsonText);

            var itemArray = obj["Items"].GetArray();
            var footerArray = obj["FooterItems"].GetArray();

            foreach (var groupValue in itemArray)
            {
                var item = new NavViewItemViewModel(groupValue.GetObject());
                item.IsFooter = false;

                Items.Add(item);
            }

            foreach (var groupValue in footerArray)
            {
                var item = new NavViewItemViewModel(groupValue.GetObject());
                item.IsFooter = true;

                FooterItems.Add(item);
            }
        }

        /// <summary>
        /// Serializes <see cref="Items"/> and <see cref="FooterItems"/>
        /// to a JSON file.
        /// </summary>
        public async Task SerializeGroupsAsync()
        {
            JsonArray array = new JsonArray();
            foreach (var item in Items)
            {
                array.Add(item.Model.GetJson());
            }

            JsonArray footerArray = new JsonArray();
            foreach (var item in FooterItems)
            {
                footerArray.Add(item.Model.GetJson());
            }

            var builder = new StringBuilder(array.ToString());

            builder.Insert(0, "{\"Items\":");
            builder.Append(",\"FooterItems\":");
            builder.Append(footerArray.ToString());
            builder.Append("}");

            StorageFile file = await ApplicationData.Current.LocalFolder.
                CreateFileAsync(_fileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, builder.ToString());
        }
        #endregion

        /// <summary>
        /// Changes the visibility of NavigationView headers based
        /// on the visibility of its items.
        /// </summary>
        /// <param name="group">Header group to check.</param>
        /// <param name="inFooter">Whether or not the header and its
        /// items are in the footer.</param>
        public void CheckHeaderVisibility(string group, bool inFooter = false)
        {
            if (group == "General")
                return;

            var items = GetItemCollection(inFooter);
            var header = items.FirstOrDefault(i => i.ItemType == NavViewItemType.Header && i.HeaderGroup == group);

            if (header != null)
            {
                foreach (NavViewItemViewModel item in items)
                {
                    if (item.ItemType == NavViewItemType.Item &&
                        item.HeaderGroup == group &&
                        item.IsVisible)
                    {
                        // An item is visible, no need to hide header
                        header.IsVisible = true;
                        return;
                    }
                }

                header.IsVisible = false;
            }
        }

        /// <summary>
        /// Changes the currently applied icon pack.
        /// </summary>
        /// <param name="newName">Name of the new icon pack. If null or "Default",
        /// go back to the default icons.</param>
        public void ChangeIconPack(string newName = null)
        {
            if (!string.IsNullOrEmpty(newName) && newName != "Default")
            {
                foreach (NavViewItemViewModel item in Items)
                {
                    if (item.ItemType == NavViewItemType.Item)
                        item.Icon = $"ms-appx:///Assets/NavigationView/{item.Id}/{newName}.png";
                }

                foreach (NavViewItemViewModel item in FooterItems)
                {
                    if (item.ItemType == NavViewItemType.Item)
                        item.Icon = $"ms-appx:///Assets/NavigationView/{item.Id}/{newName}.png";
                }
            }
            else
            {
                foreach (NavViewItemViewModel item in Items)
                {
                    if (item.ItemType == NavViewItemType.Item)
                        item.Icon = _defaultIcons[item.Id];
                }

                foreach (NavViewItemViewModel item in FooterItems)
                {
                    if (item.ItemType == NavViewItemType.Item)
                        item.Icon = _defaultIcons[item.Id];
                }
            }
        }

        /// <summary>
        /// Hides a group of NavigationView items and their header.
        /// </summary>
        /// <param name="group">Group to hide.</param>
        [RelayCommand]
        public void HideGroup(string group)
        {
            foreach (NavViewItemViewModel item in Items)
            {
                if (item.HeaderGroup == group)
                    item.IsVisible = false;
            }

            foreach (NavViewItemViewModel item in FooterItems)
            {
                if (item.HeaderGroup == group)
                    item.IsVisible = false;
            }
        }

        /// <summary>
        /// Shows a group of NavigationView items and their header.
        /// </summary>
        /// <param name="group">Group to show.</param>
        public void ShowGroup(string group)
        {
            foreach (NavViewItemViewModel item in Items)
            {
                if (item.HeaderGroup == group)
                    item.IsVisible = true;
            }

            foreach (NavViewItemViewModel item in FooterItems)
            {
                if (item.HeaderGroup == group)
                    item.IsVisible = true;
            }
        }

        /// <summary>
        /// Toggles the visibility of a NavigationView item.
        /// </summary>
        /// <param name="id">Id of the item to change.</param>
        [RelayCommand]
        public void ToggleItemVisibility(string id)
        {
            _ = TryGetItem(id, out var item);

            item.IsVisible = !item.IsVisible;
            CheckHeaderVisibility(item.HeaderGroup);
        }

        /// <summary>
        /// Changes the visibility of a NavigationView item.
        /// </summary>
        /// <param name="id">Id of the item to change.</param>
        /// <param name="vis">Whether or not the item should be visible.</param>
        public void ChangeItemVisibility(string id, bool vis)
        {
            _ = TryGetItem(id, out var item);

            item.IsVisible = vis;
            CheckHeaderVisibility(item.HeaderGroup);
        }

        /// <summary>
        /// Changes the visibility of a NavigationView header.
        /// </summary>
        /// <param name="groupName">Group name of the header to change.</param>
        /// <param name="vis">Whether or not the header should be visible.</param>
        public void ChangeHeaderVisibility(string groupName, bool vis)
            => HeaderFromGroupName(groupName).IsVisible = vis;

        /// <summary>
        /// Whether or not is an item visible.
        /// </summary>
        /// <param name="id">Id of the item to check.</param>
        /// <returns>Whether or not is the item visible.</returns>
        public bool IsItemVisible(string id)
        {
            _ = TryGetItem(id, out var item);
            return item.IsVisible;
        }

        /// <summary>
        /// Whether or not is a header visible.
        /// </summary>
        /// <param name="groupName">Group name of the header to check.</param>
        /// <returns>Whether or not is the item visible.</returns>
        public bool IsHeaderVisible(string groupName)
            => HeaderFromGroupName(groupName).IsVisible;

        #region Moving
        /// <summary>
        /// Checks if an item can be moved down.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        /// <returns>True if the item can be moved down,
        /// false otherwise.</returns>
        public bool CanMoveDown(string id)
        {
            _ = TryGetItem(id, out var item);
            var items = GetItemCollection(item.IsFooter);

            int index = items.IndexOf(item) + 1;
            if (index == items.Count)
                return false;

            var elm = items.ElementAt(index);
            return elm.HeaderGroup == item.HeaderGroup;
        }

        private void MoveItem(string id, int offset)
        {
            _ = TryGetItem(id, out var item);
            var items = GetItemCollection(item.IsFooter);

            int index = items.IndexOf(item);
            items.Move(index, index + offset);
        }

        /// <summary>
        /// Moves an item down.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        [RelayCommand]
        public void MoveDown(string id)
            => MoveItem(id, 1);

        /// <summary>
        /// Moves an item to the bottom.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        [RelayCommand]
        public void MoveToBottom(string id)
        {
            _ = TryGetItem(id, out var item);
            var items = GetItemCollection(item.IsFooter);

            int index = items.IndexOf(item);

            var lastInGroup = items.Where(i => i.HeaderGroup == item.HeaderGroup).LastOrDefault();
            items.Move(index, items.IndexOf(lastInGroup));
        }

        /// <summary>
        /// Checks if an item can be moved up.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        /// <returns>True if the item can be moved up,
        /// false otherwise.</returns>
        public bool CanMoveUp(string id)
        {
            _ = TryGetItem(id, out var item);
            var items = GetItemCollection(item.IsFooter);

            int index = items.IndexOf(item);
            if (index == 0)
                return false;

            var elm = items.ElementAt(index - 1);
            bool sameGroup = elm.HeaderGroup == item.HeaderGroup;
            bool directlyBelowHeader = sameGroup && elm.ItemType == NavViewItemType.Header;

            return sameGroup && !directlyBelowHeader;
        }

        /// <summary>
        /// Moves an item up.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        [RelayCommand]
        public void MoveUp(string id)
            => MoveItem(id, -1);

        /// <summary>
        /// Moves an item to the top.
        /// </summary>
        /// <param name="id">Item's Id.</param>
        [RelayCommand]
        public void MoveToTop(string id)
        {
            _ = TryGetItem(id, out var item);
            var items = GetItemCollection(item.IsFooter);
            int index = items.IndexOf(item);

            if (item.HeaderGroup == "General")
            {
                items.Move(index, 0);
            }
            else
            {
                var header = HeaderFromGroupName(item.HeaderGroup);
                items.Move(index, items.IndexOf(header) + 1);
            }
        }
        #endregion

        #region Finding items
        /// <summary>
        /// Tries to get an item with the specified ID.
        /// </summary>
        /// <param name="id">ID of the item.</param>
        /// <param name="item">The item if found.</param>
        /// <returns>true if the item is found, false otherwise.</returns>
        public bool TryGetItem(string id, out NavViewItemViewModel item)
        {
            item = this.Items.FirstOrDefault(i => i.Id.Equals(id));
            item ??= this.FooterItems.FirstOrDefault(i => i.Id.Equals(id));

            return item != null;
        }

        /// <summary>
        /// Gets a header based on its group name.
        /// </summary>
        /// <param name="group">The header's group name.</param>
        /// <returns>The header with the specified group name.</returns>
        public NavViewItemViewModel HeaderFromGroupName(string group)
        {
            bool predicate(NavViewItemViewModel i)
                => i.ItemType == NavViewItemType.Header && i.HeaderGroup == group;

            var match = this.Items.FirstOrDefault(predicate);
            match ??= this.FooterItems.FirstOrDefault(predicate);

            if (match == null)
                throw new ArgumentException($"Provided group name ({group}) was not found.");

            return match;
        }

        private Collection<NavViewItemViewModel> GetItemCollection(bool getFooter)
        {
            if (getFooter)
                return FooterItems;
            return Items;
        }
        #endregion
    }
}
