using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;

namespace RouteDebugger
{
    /// <summary>
    /// ActionSelectionLog store the information collected during action select simulating
    /// </summary>
    public class ActionSelectionLog
    {
        private Dictionary<HttpActionDescriptor, ActionSelectionInfo> _actionDescriptors;

        public ActionSelectionLog(IEnumerable<HttpActionDescriptor> actions)
        {
            _actionDescriptors = new Dictionary<HttpActionDescriptor, ActionSelectionInfo>();

            foreach (var each in actions)
            {
                _actionDescriptors[each] = new ActionSelectionInfo(each);
            }
        }

        public string ActionName { get; set; }

        public HttpMethod HttpMethod { get; set; }

        public ActionSelectionInfo[] ActionSelections
        {
            get
            {
                return _actionDescriptors.Values.ToArray();
            }
        }

        /// <summary>
        /// Marking aciotns as selected in one stage.
        /// </summary>
        /// <param name="actions">the actions to be marked</param>
        /// <param name="marking">the functor picking the bool property of an action to be set to true.</param>
        internal void Mark(IEnumerable<HttpActionDescriptor> actions, Action<ActionSelectionInfo> marking)
        {
            foreach (var each in actions)
            {
                ActionSelectionInfo found;
                if (_actionDescriptors.TryGetValue(each, out found))
                {
                    marking(found);
                }
            }
        }

        /// <summary>
        /// Marking actions other than the given ones as selected in one stage.
        /// </summary>
        /// <param name="actions">the actions NOT to be marked</param>
        /// <param name="marking">the functor picking the bool property of an action to be set to true.</param>
        internal void MarkOthers(IEnumerable<HttpActionDescriptor> actions, Action<ActionSelectionInfo> marking)
        {
            HashSet<HttpActionDescriptor> remaining = new HashSet<HttpActionDescriptor>(_actionDescriptors.Keys);

            foreach (var each in actions)
            {
                remaining.Remove(each);
            }

            foreach (var each in remaining)
            {
                ActionSelectionInfo found;
                if (_actionDescriptors.TryGetValue(each, out found))
                {
                    marking(found);
                }
            }
        }
    }

    /// <summary>
    /// Representing one action selection
    /// </summary>
    public class ActionSelectionInfo
    {
        public ActionSelectionInfo(HttpActionDescriptor descriptor)
        {
            this.ActionName = descriptor.ActionName;
            this.SupportedHttpMethods = descriptor.SupportedHttpMethods.ToArray();
            this.Parameters = descriptor.GetParameters().Select(p => new HttpParameterDescriptorInfo(p)).ToArray();
        }

        public string ActionName { get; set; }

        public HttpMethod[] SupportedHttpMethods { get; set; }

        public HttpParameterDescriptorInfo[] Parameters { get; set; }

        /// <summary>
        /// Is this action selected based on its action name?
        /// </summary>
        public bool? FoundByActionName { get; set; }

        /// <summary>
        /// Is this action selected based on its action name and its supported http verb 
        /// </summary>
        public bool? FoundByActionNameWithRightVerb { get; set; }

        /// <summary>
        /// Is this action selected based on its supported http verb
        /// </summary>
        public bool? FoundByVerb { get; set; }

        /// <summary>
        /// Are this action's parameters match the ones in query string
        /// </summary>
        public bool? FoundWithRightParam { get; set; }

        /// <summary>
        /// Is this action finally selected by selection attribute
        /// </summary>
        public bool? FoundWithSelectorsRun { get; set; }
    }

    /// <summary>
    /// Representing the parameters 
    /// </summary>
    public class HttpParameterDescriptorInfo
    {
        public HttpParameterDescriptorInfo(HttpParameterDescriptor descriptor)
        {
            this.ParameterName = descriptor.ParameterName;
            this.ParameterType = descriptor.ParameterType;
            this.ParameterTypeName = descriptor.ParameterType.Name;
        }

        public string ParameterName { get; set; }

        public Type ParameterType { get; set; }

        public string ParameterTypeName { get; set; }
    }
}