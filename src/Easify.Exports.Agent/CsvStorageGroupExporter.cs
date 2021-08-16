﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Easify.Exports.Agent.Notifications;
using Easify.Exports.Common;
using Easify.Exports.Csv;
using Easify.Exports.Extensions;
using Easify.Exports.Storage;
using Microsoft.Extensions.Logging;

namespace Easify.Exports.Agent
{
    public abstract class CsvStorageGroupExporter : ExporterBase
    {
        private readonly IEnumerable<IGroupItemExporter> _groupItemExporters;
        private readonly ILogger<ExporterBase> _logger;

        protected CsvStorageGroupExporter(IReportNotifierBuilder reportNotifierBuilder,
            IEnumerable<IGroupItemExporter> groupItemExporters, ILogger<ExporterBase> logger) :
            base(reportNotifierBuilder, logger)
        {
            _groupItemExporters = groupItemExporters ?? throw new ArgumentNullException(nameof(groupItemExporters));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected abstract Type[] ChildExporterTypes { get; }

        protected abstract string ExportFilePrefix { get; }

        protected override async Task<ExportResult> InternalRunAsync(ExportExecutionContext context,
            StorageTarget[] storageTargets)
        {
            _logger.LogInformation(
                $"Exporting the group data. export context: {context.ToJson()}");

            var childExporters = _groupItemExporters
                .Where(e => ChildExporterTypes.Contains(e.GetType())).ToArray();

            var exportTypes = string.Join(",", childExporters.Select(e => e.GroupItemType));
            _logger.LogInformation(
                $"Exporting the data for {exportTypes}. export context: {context.ToJson()}");

            if (childExporters.Length != ChildExporterTypes.Length)
            {
                _logger.LogWarning(
                    $"Missing exporters from runtime. Expecting {ChildExporterTypes.Length}, Found {childExporters.Length}");
                
                return ExportResult.Fail($"Missing exporters from runtime. Expecting {ChildExporterTypes.Length}, Found {childExporters.Length}");
            }
            
            _logger.LogInformation(
                $"Exporting the data for {exportTypes}. export context: {context.ToJson()}");
            
            var options = CreateExporterOptions(context, storageTargets) ??
                          CreateDefaultOptions(context, storageTargets);

            try
            {
                var tasks = childExporters.Select(e => e.RunAsync(options, storageTargets));

                var results = await Task.WhenAll(tasks);

                var (metadataFile, count) = await GenerateExportMetadataAsync(results, options, storageTargets);

                if (results.All(r => r.HasError == false)) 
                    return ExportResult.Success(metadataFile, count);
                
                var errors = results.Where(r => r.HasError).Select(r => r.HasError).ToArray();
                return ExportResult.Fail(string.Join(Environment.NewLine, errors));
            }
            catch (Exception e)
            {
                var message = $"Error in generating the exports. export context: {context.ToJson()}";
                
                _logger.LogError(message, e);
                return ExportResult.Fail(message);
            }
        }

        protected abstract Task<(string MetadataFile, int count)> 
            GenerateExportMetadataAsync(ExportResult[] results, ExporterOptions options, StorageTarget[] storageTargets);

        protected virtual ExporterOptions CreateExporterOptions(ExportExecutionContext executionContext,
            StorageTarget[] storageTargets)
        {
            return CreateDefaultOptions(executionContext, storageTargets);
        }

        private ExporterOptions CreateDefaultOptions(ExportExecutionContext executionContext,
            StorageTarget[] storageTargets)
        {
            return new ExporterOptions(executionContext.AsOfDate, storageTargets, ExportFilePrefix);
        }
    }
}