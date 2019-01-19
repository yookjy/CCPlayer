using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using System;
using System.Reflection;

namespace CCPlayer.UWP.ViewModels.Base
{
    [AttributeUsage(AttributeTargets.Field)]
    class DependencyInjectionAttribute : Attribute {}

    public abstract class CCPViewModelBase : ViewModelBase
    {
        public CCPViewModelBase()
        {
            FakeIocInstanceInitialize();
            GetIocInstance();
            CreateModel();
            RegisterMessage();
            RegisterEventHandler();
            InitializeViewModel();
        }

        private void GetIocInstance()
        {
            var fields = this.GetType().GetRuntimeFields();
            foreach(FieldInfo fi in fields)
            {
                var iocAttr = fi.GetCustomAttribute<DependencyInjectionAttribute>();
                if (iocAttr != null)
                {
                    var instance = SimpleIoc.Default.GetInstance(fi.FieldType);
                    fi.SetValue(this, instance);
                }
            }
        }

        protected abstract void FakeIocInstanceInitialize();
        protected abstract void CreateModel();
        protected abstract void RegisterEventHandler();
        protected abstract void RegisterMessage();
        protected abstract void InitializeViewModel();
    }
}
