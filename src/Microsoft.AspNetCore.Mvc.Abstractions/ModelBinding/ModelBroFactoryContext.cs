using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class ModelBroFactoryContext
    {
        public BindingInfo BindingInfo { get; set; }

        public Func<ModelBroMetadata, IModelBinder> CreateBro { get; set; }

        public bool IsTopLevelObject { get; set; }

        public ModelBroMetadata Metadata { get; set; }

        public Type ModelType { get; set; }
    }
}
