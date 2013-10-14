using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public interface ITypableMapStatusRepositoryFactory
    {
        ITypableMapStatusRepository Create(Int32 size);
    }

    public class TypableMapStatusMemoryRepositoryFactory : ITypableMapStatusRepositoryFactory
    {
        #region ITypableMapStatusRepositoryFactory メンバ

        public ITypableMapStatusRepository Create(int size)
        {
            return new TypableMapStatusMemoryRepository(size);
        }

        #endregion
    }

    public class TypableMapStatusMemoryRepositoryFactory2 : ITypableMapStatusRepositoryFactory
    {
        #region ITypableMapStatusRepositoryFactory メンバ

        public ITypableMapStatusRepository Create(int size)
        {
            return new TypableMapStatusMemoryRepository2(size);
        }

        #endregion
    }

    public class TypableMapStatusOnDemandRepositoryFactory : ITypableMapStatusRepositoryFactory
    {
        private Session _session;
        public TypableMapStatusOnDemandRepositoryFactory(Session session)
        {
            _session = session;
        }
        #region ITypableMapStatusRepositoryFactory メンバ

        public ITypableMapStatusRepository Create(int size)
        {
            return new TypableMapStatusOnDemandRepository(_session, size);
        }

        #endregion
    }
}
