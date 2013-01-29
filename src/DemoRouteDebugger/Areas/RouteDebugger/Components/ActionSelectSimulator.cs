using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using System.Web.Http.ValueProviders.Providers;

namespace RouteDebugger.Components
{
    /// <summary>
    /// Simulate the action selection and record the decision making process.
    /// 
    /// This class is basically a copy of default IHttpActionSelector implementation (DefaultActionSelector) private members,
    /// and methods where we have included logging.
    /// The private members of the DefaultActionSelector are copied here, so we can access them.
    /// 
    /// Some help internal help classes are also copied to assist the process,
    /// </summary>
    public class ActionSelectSimulator
    {
        private ReflectedHttpActionDescriptor[] _actionDescriptors;

        private IDictionary<ReflectedHttpActionDescriptor, string[]> _actionParameterNames
            = new Dictionary<ReflectedHttpActionDescriptor, string[]>();

        private ILookup<string, ReflectedHttpActionDescriptor> _actionNameMapping;

        private void Initialize(HttpControllerDescriptor controllerDesc)
        {
            MethodInfo[] allMethods = controllerDesc.ControllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            MethodInfo[] validMethods = Array.FindAll(allMethods, IsValidActionMethod);

            _actionDescriptors = new ReflectedHttpActionDescriptor[validMethods.Length];
            for (int i = 0; i < validMethods.Length; i++)
            {
                MethodInfo method = validMethods[i];
                ReflectedHttpActionDescriptor actionDescriptor = new ReflectedHttpActionDescriptor(controllerDesc, method);
                _actionDescriptors[i] = actionDescriptor;
                HttpActionBinding actionBinding = actionDescriptor.ActionBinding;

                // Building an action parameter name mapping to compare against the URI parameters coming from the request. 
                // Here we only take into account required parameters that are simple types and come from the URI.
                _actionParameterNames.Add(
                    actionDescriptor,
                    actionBinding.ParameterBindings
                        .Where(binding => !binding.Descriptor.IsOptional && TypeHelper.IsSimpleUnderlyingType(binding.Descriptor.ParameterType) && binding.WillReadUri())
                        .Select(binding => binding.Descriptor.Prefix ?? binding.Descriptor.ParameterName).ToArray());
            }

            _actionNameMapping = _actionDescriptors.ToLookup(actionDesc => actionDesc.ActionName, StringComparer.OrdinalIgnoreCase);

        }

        /// <summary>
        ///  Troy: need description here.
        /// </summary>
        /// <param name="controllerContext"></param>
        /// <returns></returns>
        public ActionSelectionLog Simulate(HttpControllerContext controllerContext)
        {
            Initialize(controllerContext.ControllerDescriptor);

            ActionSelectionLog log = new ActionSelectionLog(_actionDescriptors);

            // If the action name exists in route data, filter the action descriptors based on action name.
            ReflectedHttpActionDescriptor[] actionsFoundByMethod = null;
            var routeData = controllerContext.Request.GetRouteData();
            string actionName;
            if (routeData.Values.TryGetValue("action", out actionName))
            {
                var actionsFound = _actionNameMapping[actionName].OfType<ReflectedHttpActionDescriptor>().ToArray();

                // Filter actions based on verb.
                actionsFoundByMethod = actionsFound
                    .Where(actionDescriptor => actionDescriptor.SupportedHttpMethods.Contains(controllerContext.Request.Method))
                    .ToArray();

                log.ActionName = actionName;
                log.Mark(actionsFound, info => info.FoundByActionName = true);
                log.MarkOthers(actionsFound, info => info.FoundByActionName = false);

                log.Mark(actionsFound, info => info.FoundByActionNameWithRightVerb = false);
                log.Mark(actionsFoundByMethod, info => info.FoundByActionNameWithRightVerb = true);
            }
            else
            {
                log.ActionName = string.Empty;

                // If action name doesn't exist, find actions based on HTTP verb.
                log.HttpMethod = controllerContext.Request.Method;

                if (string.IsNullOrEmpty(actionName))
                {
                    actionsFoundByMethod = FindActionsForVerb(log.HttpMethod);

                    log.Mark(actionsFoundByMethod, info => info.FoundByVerb = true);
                }
            }

            // If no action is found at this stage a failure must happen.
            if (actionsFoundByMethod != null && actionsFoundByMethod.Length != 0)
            {
                // filter the actions by parameters matching
                var actionsFilterByParam = FindActionUsingRouteAndQueryParameters(
                    controllerContext,
                    actionsFoundByMethod,
                    !string.IsNullOrEmpty(actionName)).ToArray();
                log.Mark(actionsFoundByMethod, info => info.FoundWithRightParam = false);
                log.Mark(actionsFilterByParam, info => info.FoundWithRightParam = true);

                // filter the actions by selection filters
                var actionsFilterBySelectors = RunSelectionFilters(controllerContext, actionsFilterByParam).ToArray();
                log.Mark(actionsFilterByParam, info => info.FoundWithSelectorsRun = false);
                log.Mark(actionsFilterBySelectors, info => info.FoundWithSelectorsRun = true);
            }

            return log;
        }


        /// <summary>
        /// This is a copy of the private ApiControllerActionSelector.FindActionsForVerb. It doesn't use the cache
        /// but copies the contents of the FindActionsForVerbWorker method.
        /// </summary>
        private ReflectedHttpActionDescriptor[] FindActionsForVerb(HttpMethod verb)
        {
            List<ReflectedHttpActionDescriptor> listMethods = new List<ReflectedHttpActionDescriptor>();

            foreach (ReflectedHttpActionDescriptor descriptor in _actionDescriptors)
            {
                if (descriptor.SupportedHttpMethods.Contains(verb))
                {
                    listMethods.Add(descriptor);
                }
            }

            return listMethods.ToArray();
        }

        /// <summary>
        /// This is an exact copy from ApiControllerActionSelector.
        /// </summary>
        private IEnumerable<ReflectedHttpActionDescriptor> FindActionUsingRouteAndQueryParameters(
            HttpControllerContext controllerContext,
            IEnumerable<ReflectedHttpActionDescriptor> actionsFound,
            bool hasActionRouteKey)
        {
            IDictionary<string, object> routeValues = controllerContext.RouteData.Values;
            HashSet<string> routeParameterNames = new HashSet<string>(routeValues.Keys, StringComparer.OrdinalIgnoreCase);
            routeParameterNames.Remove("controller");
            if (hasActionRouteKey)
            {
                routeParameterNames.Remove("action");
            }

            HttpRequestMessage request = controllerContext.Request;
            Uri requestUri = request.RequestUri;
            bool hasQueryParameters = requestUri != null && !String.IsNullOrEmpty(requestUri.Query);
            bool hasRouteParameters = routeParameterNames.Count != 0;

            if (hasRouteParameters || hasQueryParameters)
            {
                var combinedParameterNames = new HashSet<string>(routeParameterNames, StringComparer.OrdinalIgnoreCase);
                if (hasQueryParameters)
                {
                    foreach (var queryNameValuePair in request.GetQueryNameValuePairs())
                    {
                        combinedParameterNames.Add(queryNameValuePair.Key);
                    }
                }

                // action parameters is a subset of route parameters and query parameters
                actionsFound = actionsFound.Where(descriptor => IsSubset(_actionParameterNames[descriptor], combinedParameterNames));

                if (actionsFound.Count() > 1)
                {
                    // select the results that match the most number of required parameters 
                    actionsFound = actionsFound
                        .GroupBy(descriptor => _actionParameterNames[descriptor].Length)
                        .OrderByDescending(g => g.Key)
                        .First();
                }
            }
            else
            {
                // return actions with no parameters
                actionsFound = actionsFound.Where(descriptor => _actionParameterNames[descriptor].Length == 0);
            }

            return actionsFound;
        }

        /// <summary>
        /// This is an exact copy from ApiControllerActionSelector.
        /// </summary>
        private static bool IsValidActionMethod(MethodInfo methodInfo)
        {
            if (methodInfo.IsSpecialName)
            {
                // not a normal method, e.g. a constructor or an event
                return false;
            }

            if (methodInfo.GetBaseDefinition().DeclaringType.IsAssignableFrom(TypeHelper.ApiControllerType))
            {
                // is a method on Object, IHttpController, ApiController
                return false;
            }

            return true;
        }

        /// <summary>
        /// This is an exact copy from ApiControllerActionSelector.
        /// </summary>
        private static bool IsSubset(string[] actionParameters, HashSet<string> routeAndQueryParameters)
        {
            foreach (string actionParameter in actionParameters)
            {
                if (!routeAndQueryParameters.Contains(actionParameter))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Replace the private method from ApiControllerActionSelector.
        /// 
        /// The IActionMethodSelector interface used in the method is internal so we must make a copy. 
        /// CacheAttrsIActionMethodSelector is also internal.
        /// The default implementation of IActionMethodSelector finds methods marked with the NonActionAttribute, so the
        /// code below is converted to directly filter out methods with that attribute.
        /// </summary>
        private static List<ReflectedHttpActionDescriptor> RunSelectionFilters(
            HttpControllerContext controllerContext,
            IEnumerable<HttpActionDescriptor> descriptorsFound)
        {
            // remove all methods which are opting out of this request
            // to opt out, at least one attribute defined on the method must return false

            List<ReflectedHttpActionDescriptor> matchesWithSelectionAttributes = null;
            List<ReflectedHttpActionDescriptor> matchesWithoutSelectionAttributes = new List<ReflectedHttpActionDescriptor>();

            foreach (ReflectedHttpActionDescriptor actionDescriptor in descriptorsFound)
            {
                var attrs = actionDescriptor.GetCustomAttributes<NonActionAttribute>().ToArray();
                //IActionMethodSelector[] attrs = actionDescriptor.CacheAttrsIActionMethodSelector;
                if (attrs.Length == 0)
                {
                    matchesWithoutSelectionAttributes.Add(actionDescriptor);
                }
                else
                {
                    // The following code will never run (it's always false), so it's commented out.

                    //bool match = Array.TrueForAll(attrs, selector => selector.IsValidForRequest(controllerContext, actionDescriptor.MethodInfo));
                    //if (match)
                    //{
                    //    if (matchesWithSelectionAttributes == null)
                    //    {
                    //        matchesWithSelectionAttributes = new List<ReflectedHttpActionDescriptor>();
                    //    }
                    //    matchesWithSelectionAttributes.Add(actionDescriptor);
                    //}
                }
            }

            // if a matching action method had a selection attribute, consider it more specific than a matching action method
            // without a selection attribute
            if ((matchesWithSelectionAttributes != null) && (matchesWithSelectionAttributes.Count > 0))
            {
                return matchesWithSelectionAttributes;
            }
            else
            {
                return matchesWithoutSelectionAttributes;
            }
        }
    }

    /// <summary>
    /// A copy of HttpParameterBindingExtensions.cs with one change. HttpParameterBindingExtensions.WillReadUri calls the internal
    /// interface IUriValueProvderFactory, so that code is also in this method.
    /// </summary>
    internal static class HttpParameterBindingExtensions
    {
        public static bool WillReadUri(this HttpParameterBinding parameterBinding)
        {
            if (parameterBinding == null)
            {
                throw new ArgumentNullException("parameterBinding");
            }

            IValueProviderParameterBinding valueProviderParameterBinding = parameterBinding as IValueProviderParameterBinding;
            if (valueProviderParameterBinding != null)
            {
                IEnumerable<ValueProviderFactory> valueProviderFactories = valueProviderParameterBinding.ValueProviderFactories;
                // since The interface IUriValueProvderFactory is internal, following line of codes is altered
                // if (valueProviderFactories.Any() && valueProviderFactories.All(factory => factory is IUriValueProviderFactory))
                if (valueProviderFactories.Any() && valueProviderFactories.All(factory => (factory is QueryStringValueProviderFactory) || (factory is RouteDataValueProviderFactory)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// An exact copy of the TryGetValue method from  DictionaryExtensions.cs.
    /// </summary>
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value of <typeparamref name="T"/> associated with the specified key or <c>default</c> value if
        /// either the key is not present or the value is not of type <typeparamref name="T"/>. 
        /// </summary>
        /// <typeparam name="T">The type of the value associated with the specified key.</typeparam>
        /// <param name="collection">The <see cref="IDictionary{TKey,TValue}"/> instance where <c>TValue</c> is <c>object</c>.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns><c>true</c> if key was found, value is non-null, and value is of type <typeparamref name="T"/>; otherwise false.</returns>
        public static bool TryGetValue<T>(this IDictionary<string, object> collection, string key, out T value)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            object valueObj;
            if (collection.TryGetValue(key, out valueObj))
            {
                if (valueObj is T)
                {
                    value = (T)valueObj;
                    return true;
                }
            }

            value = default(T);
            return false;
        }
    }
}