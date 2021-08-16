// This software is part of the Easify.Exports Library
// Copyright (C) 2021 Intermediate Capital Group
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 

using System;
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
    public abstract class CsvStorageExporter<T> : ExporterBase where T : class
    {
        private readonly IFileExporter _fileExporter;
        private readonly ILogger<CsvStorageExporter<T>> _logger;

        protected CsvStorageExporter(IFileExporter fileExporter, IReportNotifierBuilder reportNotifierBuilder,
            ILogger<CsvStorageExporter<T>> logger) : base(reportNotifierBuilder, logger)
        {
            _fileExporter = fileExporter ?? throw new ArgumentNullException(nameof(fileExporter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected abstract string ExportFilePrefix { get; }

        protected override async Task<ExportResult> InternalRunAsync(ExportExecutionContext context,
            StorageTarget[] storageTargets)
        {
            _logger.LogInformation($"Loading the list of {typeof(T)}. export context: {context.ToJson()}");

            var data = await PrepareDataAsync(context);
            if (data == null)
                return ExportResult.Fail("Invalid data from the source.");

            var enumerable = data as T[] ?? data.ToArray();
            _logger.LogInformation(
                $"Exporting {enumerable.Length} {typeof(T)} in the list. export context: {context.ToJson()}");

            var options = CreateExporterOptions(context, storageTargets, enumerable) ??
                          CreateDefaultOptions(context, storageTargets);

            return await _fileExporter.ExportAsync(enumerable, options);
        }

        protected abstract Task<IEnumerable<T>> PrepareDataAsync(ExportExecutionContext executionContext);

        protected virtual ExporterOptions CreateExporterOptions(ExportExecutionContext executionContext,
            StorageTarget[] storageTargets,
            IEnumerable<T> data)
        {
            return CreateDefaultOptions(executionContext, storageTargets);
        }

        private ExporterOptions CreateDefaultOptions(ExportExecutionContext executionContext,
            StorageTarget[] storageTargets)
        {
            return new(executionContext.AsOfDate, storageTargets, ExportFilePrefix);
        }
    }
}