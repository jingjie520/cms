﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SS.CMS.Enums;
using SS.CMS.Models;
using SS.CMS.Repositories;
using SS.CMS.Services;
using SS.CMS.Utils;
using SS.CMS.Utils.Atom.Atom.Core;

namespace SS.CMS.Core.Serialization.Components
{
    internal class TableStyleIe
    {
        private readonly string _directoryPath;
        private readonly int _userId;
        private readonly IPluginManager _pluginManager;
        private readonly ICreateManager _createManager;
        private readonly IPathManager _pathManager;
        private readonly IFileManager _fileManager;
        private readonly ITableManager _tableManager;
        private readonly ISiteRepository _siteRepository;
        private readonly IChannelRepository _channelRepository;
        private readonly IChannelGroupRepository _channelGroupRepository;
        private readonly IContentGroupRepository _contentGroupRepository;
        private readonly ISpecialRepository _specialRepository;
        private readonly ITableStyleRepository _tableStyleRepository;
        private readonly ITemplateRepository _templateRepository;

        public TableStyleIe(string directoryPath, int userId)
        {
            _directoryPath = directoryPath;
            _userId = userId;
        }

        public async Task ExportTableStylesAsync(int siteId, string tableName)
        {
            var allRelatedIdentities = await _channelRepository.GetChannelIdListAsync(siteId);
            allRelatedIdentities.Insert(0, 0);
            var tableStyleInfoWithItemsDict = await _tableManager.GetTableStyleInfoWithItemsDictionaryAsync(tableName, allRelatedIdentities);
            if (tableStyleInfoWithItemsDict == null || tableStyleInfoWithItemsDict.Count <= 0) return;

            var styleDirectoryPath = PathUtils.Combine(_directoryPath, tableName);
            DirectoryUtils.CreateDirectoryIfNotExists(styleDirectoryPath);

            foreach (var attributeName in tableStyleInfoWithItemsDict.Keys)
            {
                var tableStyleInfoWithItemList = tableStyleInfoWithItemsDict[attributeName];
                if (tableStyleInfoWithItemList == null || tableStyleInfoWithItemList.Count <= 0) continue;

                var attributeNameDirectoryPath = PathUtils.Combine(styleDirectoryPath, attributeName);
                DirectoryUtils.CreateDirectoryIfNotExists(attributeNameDirectoryPath);

                foreach (var tableStyleInfo in tableStyleInfoWithItemList)
                {
                    //仅导出当前系统内的表样式
                    if (tableStyleInfo.RelatedIdentity != 0)
                    {
                        if (!await _channelRepository.IsAncestorOrSelfAsync(siteId, tableStyleInfo.RelatedIdentity))
                        {
                            continue;
                        }
                    }
                    var filePath = attributeNameDirectoryPath + PathUtils.SeparatorChar + tableStyleInfo.Id + ".xml";
                    var feed = await ExportTableStyleInfoAsync(_channelRepository, tableStyleInfo);
                    if (tableStyleInfo.StyleItems != null && tableStyleInfo.StyleItems.Count > 0)
                    {
                        foreach (var styleItemInfo in tableStyleInfo.StyleItems)
                        {
                            var entry = ExportTableStyleItemInfo(styleItemInfo);
                            feed.Entries.Add(entry);
                        }
                    }
                    feed.Save(filePath);
                }
            }
        }

        private static async Task<AtomFeed> ExportTableStyleInfoAsync(IChannelRepository channelRepository, TableStyleInfo tableStyleInfo)
        {
            var feed = AtomUtility.GetEmptyFeed();

            AtomUtility.AddDcElement(feed.AdditionalElements, new List<string> { nameof(TableStyleInfo.Id), "TableStyleID" }, tableStyleInfo.Id.ToString());
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.RelatedIdentity), tableStyleInfo.RelatedIdentity.ToString());
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.TableName), tableStyleInfo.TableName);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.AttributeName), tableStyleInfo.AttributeName);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.Taxis), tableStyleInfo.Taxis.ToString());
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.DisplayName), tableStyleInfo.DisplayName);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.HelpText), tableStyleInfo.HelpText);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.IsVisibleInList), tableStyleInfo.IsVisibleInList.ToString());
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.Type), tableStyleInfo.Type.Value);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.DefaultValue), tableStyleInfo.DefaultValue);
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.IsHorizontal), tableStyleInfo.IsHorizontal.ToString());
            //ExtendValues
            AtomUtility.AddDcElement(feed.AdditionalElements, nameof(TableStyleInfo.ExtendValues), tableStyleInfo.ExtendValues);

            //保存此栏目样式在系统中的排序号
            var orderString = string.Empty;
            if (tableStyleInfo.RelatedIdentity != 0)
            {
                orderString = await channelRepository.GetOrderStringInSiteAsync(tableStyleInfo.RelatedIdentity);
            }

            AtomUtility.AddDcElement(feed.AdditionalElements, "OrderString", orderString);

            return feed;
        }

        private static AtomEntry ExportTableStyleItemInfo(TableStyleItemInfo styleItemInfo)
        {
            var entry = AtomUtility.GetEmptyEntry();

            AtomUtility.AddDcElement(entry.AdditionalElements, new List<string> { nameof(TableStyleItemInfo.Id), "TableStyleItemID" }, styleItemInfo.Id.ToString());
            AtomUtility.AddDcElement(entry.AdditionalElements, new List<string> { nameof(TableStyleItemInfo.TableStyleId), "TableStyleID" }, styleItemInfo.TableStyleId.ToString());
            AtomUtility.AddDcElement(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemTitle), styleItemInfo.ItemTitle);
            AtomUtility.AddDcElement(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemValue), styleItemInfo.ItemValue);
            AtomUtility.AddDcElement(entry.AdditionalElements, nameof(TableStyleItemInfo.IsSelected), styleItemInfo.IsSelected.ToString());

            return entry;
        }

        public static async Task SingleExportTableStylesAsync(ITableManager tableManager, IChannelRepository channelRepository, string tableName, int siteId, int relatedIdentity, string styleDirectoryPath)
        {
            var channelInfo = await channelRepository.GetChannelInfoAsync(relatedIdentity);
            var relatedIdentities = tableManager.GetRelatedIdentities(channelInfo);

            DirectoryUtils.DeleteDirectoryIfExists(styleDirectoryPath);
            DirectoryUtils.CreateDirectoryIfNotExists(styleDirectoryPath);

            var styleInfoList = await tableManager.GetStyleInfoListAsync(tableName, relatedIdentities);
            foreach (var tableStyleInfo in styleInfoList)
            {
                var filePath = PathUtils.Combine(styleDirectoryPath, tableStyleInfo.AttributeName + ".xml");
                var feed = await ExportTableStyleInfoAsync(channelRepository, tableStyleInfo);
                var styleItems = tableStyleInfo.StyleItems;
                if (styleItems != null && styleItems.Count > 0)
                {
                    foreach (var styleItemInfo in styleItems)
                    {
                        var entry = ExportTableStyleItemInfo(styleItemInfo);
                        feed.Entries.Add(entry);
                    }
                }
                feed.Save(filePath);
            }
        }

        public static async Task SingleExportTableStylesAsync(ITableManager tableManager, IChannelRepository channelRepository, string tableName, string styleDirectoryPath)
        {
            var relatedIdentities = new List<int> { 0 };

            DirectoryUtils.DeleteDirectoryIfExists(styleDirectoryPath);
            DirectoryUtils.CreateDirectoryIfNotExists(styleDirectoryPath);

            var styleInfoList = await tableManager.GetStyleInfoListAsync(tableName, relatedIdentities);
            foreach (var tableStyleInfo in styleInfoList)
            {
                var filePath = PathUtils.Combine(styleDirectoryPath, tableStyleInfo.AttributeName + ".xml");
                var feed = await ExportTableStyleInfoAsync(channelRepository, tableStyleInfo);
                var styleItems = tableStyleInfo.StyleItems;
                if (styleItems != null && styleItems.Count > 0)
                {
                    foreach (var styleItemInfo in styleItems)
                    {
                        var entry = ExportTableStyleItemInfo(styleItemInfo);
                        feed.Entries.Add(entry);
                    }
                }
                feed.Save(filePath);
            }
        }

        public static async Task SingleImportTableStyleAsync(ITableStyleRepository tableStyleRepository, string tableName, string styleDirectoryPath, int relatedIdentity)
        {
            if (!DirectoryUtils.IsDirectoryExists(styleDirectoryPath)) return;

            var filePaths = DirectoryUtils.GetFilePaths(styleDirectoryPath);
            foreach (var filePath in filePaths)
            {
                var feed = AtomFeed.Load(FileUtils.GetFileStreamReadOnly(filePath));

                var attributeName = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.AttributeName));
                var taxis = TranslateUtils.ToInt(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.Taxis)), 0);
                var displayName = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.DisplayName));
                var helpText = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.HelpText));
                var isVisibleInList = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.IsVisibleInList)));
                var inputType = InputType.Parse(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.Type)));
                var defaultValue = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.DefaultValue));
                var isHorizontal = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.IsHorizontal)));
                //ExtendValues
                var extendValues = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.ExtendValues));

                var styleInfo = new TableStyleInfo
                {
                    RelatedIdentity = relatedIdentity,
                    TableName = tableName,
                    AttributeName = attributeName,
                    Taxis = taxis,
                    DisplayName = displayName,
                    HelpText = helpText,
                    IsVisibleInList = isVisibleInList,
                    Type = inputType,
                    DefaultValue = defaultValue,
                    IsHorizontal = isHorizontal,
                    ExtendValues = extendValues
                };

                var styleItems = new List<TableStyleItemInfo>();
                foreach (AtomEntry entry in feed.Entries)
                {
                    var itemTitle = AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemTitle));
                    var itemValue = AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemValue));
                    var isSelected = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.IsSelected)));

                    var itemInfo = new TableStyleItemInfo
                    {
                        ItemTitle = itemTitle,
                        ItemValue = itemValue,
                        IsSelected = isSelected
                    };

                    styleItems.Add(itemInfo);
                }

                if (styleItems.Count > 0)
                {
                    styleInfo.StyleItems = styleItems;
                }

                if (await tableStyleRepository.IsExistsAsync(relatedIdentity, tableName, attributeName))
                {
                    await tableStyleRepository.DeleteAsync(relatedIdentity, tableName, attributeName);
                }
                await tableStyleRepository.InsertAsync(styleInfo);
            }
        }

        public async Task ImportTableStylesAsync(int siteId)
        {
            if (!DirectoryUtils.IsDirectoryExists(_directoryPath)) return;

            var importObject = new ImportObject();
            await importObject.LoadAsync(siteId, _userId);
            var tableNameCollection = await importObject.GetTableNameCacheAsync();

            var styleDirectoryPaths = DirectoryUtils.GetDirectoryPaths(_directoryPath);

            foreach (var styleDirectoryPath in styleDirectoryPaths)
            {
                var tableName = PathUtils.GetDirectoryName(styleDirectoryPath, false);
                if (tableName == "siteserver_PublishmentSystem")
                {
                    tableName = _siteRepository.TableName;
                }
                if (!string.IsNullOrEmpty(tableNameCollection?[tableName]))
                {
                    tableName = tableNameCollection[tableName];
                }

                var attributeNamePaths = DirectoryUtils.GetDirectoryPaths(styleDirectoryPath);
                foreach (var attributeNamePath in attributeNamePaths)
                {
                    var attributeName = PathUtils.GetDirectoryName(attributeNamePath, false);
                    var filePaths = DirectoryUtils.GetFilePaths(attributeNamePath);
                    foreach (var filePath in filePaths)
                    {
                        var feed = AtomFeed.Load(FileUtils.GetFileStreamReadOnly(filePath));

                        var taxis = TranslateUtils.ToInt(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.Taxis)), 0);
                        var displayName = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.DisplayName));
                        var helpText = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.HelpText));
                        var isVisibleInList = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.IsVisibleInList)));
                        var inputType = InputType.Parse(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.Type)));
                        var defaultValue = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.DefaultValue));
                        var isHorizontal = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.IsHorizontal)));
                        var extendValues = AtomUtility.GetDcElementContent(feed.AdditionalElements, nameof(TableStyleInfo.ExtendValues));

                        var orderString = AtomUtility.GetDcElementContent(feed.AdditionalElements, "OrderString");

                        var relatedIdentity = !string.IsNullOrEmpty(orderString) ? await _channelRepository.GetIdAsync(siteId, orderString) : siteId;

                        if (relatedIdentity <= 0 || await _tableStyleRepository.IsExistsAsync(relatedIdentity, tableName, attributeName)) continue;

                        var styleInfo = new TableStyleInfo
                        {
                            RelatedIdentity = relatedIdentity,
                            TableName = tableName,
                            AttributeName = attributeName,
                            Taxis = taxis,
                            DisplayName = displayName,
                            HelpText = helpText,
                            IsVisibleInList = isVisibleInList,
                            Type = inputType,
                            DefaultValue = defaultValue,
                            IsHorizontal = isHorizontal,
                            ExtendValues = extendValues
                        };

                        var styleItems = new List<TableStyleItemInfo>();
                        foreach (AtomEntry entry in feed.Entries)
                        {
                            var itemTitle = AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemTitle));
                            var itemValue = AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.ItemValue));
                            var isSelected = TranslateUtils.ToBool(AtomUtility.GetDcElementContent(entry.AdditionalElements, nameof(TableStyleItemInfo.IsSelected)));

                            var itemInfo = new TableStyleItemInfo
                            {
                                ItemTitle = itemTitle,
                                ItemValue = itemValue,
                                IsSelected = isSelected
                            };
                            styleItems.Add(itemInfo);
                        }

                        if (styleItems.Count > 0)
                        {
                            styleInfo.StyleItems = styleItems;
                        }

                        await _tableStyleRepository.InsertAsync(styleInfo);
                    }
                }
            }
        }

    }
}
