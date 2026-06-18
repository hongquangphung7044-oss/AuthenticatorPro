// Copyright (C) 2022 jmh
// SPDX-License-Identifier: GPL-3.0-only

using Android.Content;
using AuthenticatorPro.Droid.Shared.Query;
using AuthenticatorPro.Shared.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AuthenticatorPro.WearOS.Data
{
    internal class LocalAuthDatabase
    {
        private const string FileName = "authenticators.json";
        private const string BackupExtension = ".bak";
        private static readonly object Lock = new();

        private readonly string _filePath;
        private List<WearAuthenticator> _items;

        public int Count
        {
            get { lock (Lock) { return _items.Count; } }
        }

        public LocalAuthDatabase(Context context)
        {
            _filePath = Path.Combine(context.FilesDir.AbsolutePath, FileName);
            _items = new List<WearAuthenticator>();
        }

        public async Task Init()
        {
            if (!File.Exists(_filePath))
                return;

            var json = await File.ReadAllTextAsync(_filePath);

            lock (Lock)
            {
                _items = JsonConvert.DeserializeObject<List<WearAuthenticator>>(json)
                         ?? new List<WearAuthenticator>();
            }
        }

        public List<WearAuthenticator> GetAll()
        {
            lock (Lock)
            {
                return new List<WearAuthenticator>(_items);
            }
        }

        public WearAuthenticator Get(int index)
        {
            lock (Lock)
            {
                return index >= 0 && index < _items.Count ? _items[index] : null;
            }
        }

        public async Task Add(WearAuthenticator auth)
        {
            lock (Lock)
            {
                _items.Add(auth);
            }

            await Flush();
        }

        public async Task Insert(int index, WearAuthenticator auth)
        {
            lock (Lock)
            {
                _items.Insert(index, auth);
            }

            await Flush();
        }

        public async Task RemoveAt(int index)
        {
            lock (Lock)
            {
                if (index >= 0 && index < _items.Count)
                    _items.RemoveAt(index);
            }

            await Flush();
        }

        public async Task Replace(List<WearAuthenticator> items)
        {
            lock (Lock)
            {
                _items = new List<WearAuthenticator>(items);
            }

            await Flush();
        }

        public int FindIndex(System.Predicate<WearAuthenticator> predicate)
        {
            lock (Lock)
            {
                return _items.FindIndex(predicate);
            }
        }

        private async Task Flush()
        {
            string json;

            lock (Lock)
            {
                json = JsonConvert.SerializeObject(_items);
            }

            if (File.Exists(_filePath))
            {
                var bak = _filePath + BackupExtension;
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(_filePath, bak);
            }

            await File.WriteAllTextAsync(_filePath, json);

            var oldBak = _filePath + BackupExtension;
            if (File.Exists(oldBak)) File.Delete(oldBak);
        }
    }
}