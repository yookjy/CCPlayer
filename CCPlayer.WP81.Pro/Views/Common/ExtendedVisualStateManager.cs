using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.Views.Common
{
    public class ExtendedVisualStateManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement stateGroupsRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if ((group == null) || (state == null))
            {
                return false;
            }

            if (control == null)
            {
                control = new ContentControl();
            }

//            System.Diagnostics.Debug.WriteLine("코어 컨트롤 : " + control.Name + ", element name : " + stateGroupsRoot.Name + ", group : " + group.Name + ", state : " + stateName);
            return base.GoToStateCore(control, stateGroupsRoot, stateName, group, state, useTransitions);
        }

        public static bool GoToElementState(FrameworkElement element, object targetObject, string stateName, bool useTransitions)
        {
            var root = FindNearestStatefulFrameworkElement(element);

            var customVisualStateManager = VisualStateManager.GetCustomVisualStateManager(root) as ExtendedVisualStateManager;

            return ((customVisualStateManager != null) && customVisualStateManager.GoToStateInternal(root, targetObject, stateName, useTransitions));
        }

        private static FrameworkElement FindNearestStatefulFrameworkElement(FrameworkElement element)
        {
            while (element != null && VisualStateManager.GetCustomVisualStateManager(element) == null)
            {
                element = element.Parent as FrameworkElement;
            }

            return element;
        }

        private bool GoToStateInternal(FrameworkElement stateGroupsRoot, object targetObject, string stateName, bool useTransitions)
        {
            VisualStateGroup group;
            VisualState state;

            return (TryGetState(stateGroupsRoot, stateName, out group, out state) && this.GoToStateCore((Control)targetObject, stateGroupsRoot, stateName, group, state, useTransitions));
        }

        private static bool TryGetState(FrameworkElement element, string stateName, out VisualStateGroup group, out VisualState state)
        {
            group = null;
            state = null;

            foreach (VisualStateGroup group2 in VisualStateManager.GetVisualStateGroups(element))
            {
                foreach (VisualState state2 in group2.States)
                {
                    if (state2.Name == stateName)
                    {
                        //System.Diagnostics.Debug.WriteLine(stateName + "=> element name : " + element.Name + ", group : " + group2.Name);
                        group = group2;
                        state = state2;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
