/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Util;

namespace QuantConnect.Data.Shortable
{
    /// <summary>
    /// Sources easy-to-borrow (ETB) data from the local disk for the given brokerage
    /// </summary>
    public class LocalDiskShortableProvider : IShortableProvider
    {
        private readonly DirectoryInfo _shortableDataDirectory;
        private IDataProvider _dataProvider =
            Composer.Instance.GetExportedValueByTypeName<IDataProvider>(Config.Get("data-provider",
                "DefaultDataProvider"));

        /// <summary>
        /// Creates an instance of the class. Establishes the directory to read from.
        /// </summary>
        /// <param name="securityType">SecurityType to read data</param>
        /// <param name="brokerage">Brokerage to read ETB data</param>
        /// <param name="market">Market to read ETB data</param>
        public LocalDiskShortableProvider(SecurityType securityType, string brokerage, string market)
        {
            var shortableDataDirectory = Path.Combine(Globals.DataFolder, securityType.SecurityTypeToLower(), market, "shortable", brokerage.ToLowerInvariant());
            _shortableDataDirectory = Directory.CreateDirectory(shortableDataDirectory);
        }

        /// <summary>
        /// Gets a list of all shortable Symbols, including the quantity shortable as a Dictionary.
        /// </summary>
        /// <param name="localTime">The algorithm's local time</param>
        /// <returns>Symbol/quantity shortable as a Dictionary. Returns null if no entry data exists for this date or brokerage</returns>
        public Dictionary<Symbol, long> AllShortableSymbols(DateTime localTime)
        {
            var allSymbols = new Dictionary<Symbol, long>();

            // Check backwards up to one week to see if we can source a previous file.
            // If not, then we return a list of all Symbols with quantity set to zero.
            var i = 0;
            while (i <= 7)
            {
                var shortableListFile = Path.Combine(_shortableDataDirectory.FullName, "dates", $"{localTime.AddDays(-i):yyyyMMdd}.csv");

                foreach (var line in _dataProvider.ReadLines(shortableListFile))
                {
                    var csv = line.Split(',');
                    var ticker = csv[0];

                    var symbol = new Symbol(
                            SecurityIdentifier.GenerateEquity(ticker, QuantConnect.Market.USA,
                                mappingResolveDate: localTime), ticker);
                    var quantity = Parse.Long(csv[1]);

                    allSymbols[symbol] = quantity;
                }

                if (allSymbols.Count > 0)
                {
                    return allSymbols;
                }

                i++;
            }

            // Return our empty dictionary if we did not find a file to extract
            return allSymbols;
        }

        /// <summary>
        /// Gets the quantity shortable for the Symbol at the given date.
        /// </summary>
        /// <param name="symbol">Symbol to lookup shortable quantity</param>
        /// <param name="localTime">Time of the algorithm</param>
        /// <returns>Quantity shortable. Null if the data for the brokerage/date does not exist.</returns>
        public long? ShortableQuantity(Symbol symbol, DateTime localTime)
        {
            if (_shortableDataDirectory == null)
            {
                return 0;
            }

            // Implicitly trusts that Symbol.Value has been mapped and updated to the latest ticker
            var shortableSymbolFile = Path.Combine(_shortableDataDirectory.FullName, "symbols", $"{symbol.Value.ToLowerInvariant()}.csv");

            var localDate = localTime.Date;
            foreach (var line in _dataProvider.ReadLines(shortableSymbolFile))
            {
                var csv = line.Split(',');
                var date = Parse.DateTimeExact(csv[0], "yyyyMMdd");

                if (localDate == date)
                {
                    var quantity = Parse.Long(csv[1]);
                    return quantity;
                }
            }

            // Any missing entry will be considered to be unshortable.
            return 0;
        }
    }
}
