﻿using System;
using System.Threading.Tasks;
#if WINDOWS_UWP
using System.Collections.Specialized;
#endif
using LiteDB.Platform;
using Windows.Storage;
using LiteDB.Platform;

namespace LiteDB.Universal81
{
    public class LitePlatformWindowsStore : ILitePlatform
    {
        private readonly StorageFolder m_folder;

        private readonly LazyLoad<IFileHandler> _fileHandler;
        private readonly LazyLoad<IReflectionHandler> _reflectionHandler;

        public LitePlatformWindowsStore(StorageFolder folder)
        {
            _fileHandler = new LazyLoad<IFileHandler>(() => new FileHandlerWindowsStore(folder));
            _reflectionHandler = new LazyLoad<IReflectionHandler>(() => new ExpressionReflectionHandler());

            AddNameCollectionToMapper();
        }

        public IFileHandler FileHandler { get { return _fileHandler.Value; } }

        public IReflectionHandler ReflectionHandler { get { return _reflectionHandler.Value; } }

        public IEncryption GetEncryption(string password)
        {
            throw new NotImplementedException();
        }

        public void WaitFor(int milliseconds)
        {
            AsyncHelpers.RunSync(() => Task.Delay(milliseconds));
        }

        public void AddNameCollectionToMapper()
        {
#if WINDOWS_UWP
            BsonMapper.Global.RegisterType(
               nv =>
               {
                   var doc = new BsonDocument();

                   foreach (var key in nv.AllKeys)
                   {
                       doc[key] = nv[key];
                   }

                   return doc;
               },

               bson =>
               {
                   var nv = new NameValueCollection();
                   var doc = bson.AsDocument;

                   foreach (var key in doc.Keys)
                   {
                       nv[key] = doc[key].AsString;
                   }

                   return nv;
               }
            );
#endif
        }
    }
}
