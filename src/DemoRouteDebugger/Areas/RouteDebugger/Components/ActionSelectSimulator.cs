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
    /// Simulating the action selecting and record the decision making process.
    /// 
    /// This class is basically a mimic of default IHttpActionSelector implementation. Unfortunately, the logic of DefaultActionSelector is
    /// sealed in a private class, which preventing others from exposing any meaningful information. Large portion of the codes are copied
    /// here to ensure the same selecting process is gone through.
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

                // Building an action parameter name mapping to compare against the URI parameters coming from the request. Here we only take into account required parameters that are simple types and come from URI.
                _actionParameterNames.Add(
                    actionDescriptor,
                    actionBinding.ParameterBindings
                        .Where(binding => !binding.Descriptor.IsOptional && TypeHelper.IsSimpleUnderlyingType(binding.Descriptor.ParameterType) && binding.WillReadUri())
                        .Select(binding => binding.Descriptor.Prefix ?? binding.Descriptor.ParameterName).ToArray());
            }

            _actionNameMapping = _actionDescriptors.ToLookup(actionDesc => actionDesc.ActionName, StringComparer.OrdinalIgnoreCase);

        }

        public ActionSelectionLog Simulate(HttpControllerContext controllerContext)
        {
            Initialize(controllerContext.ControllerDescriptor);

            ActionSelectionLog log = new ActionSelectionLog(_actionDescriptors);

            // if action name exists in route data, filter the action descriptors based on action name
            ReflectedHttpActionDescriptor[] actionsFoundByMethod = null;
            var routeData = controllerContext.Request.GetRouteData();
            string actionName;
            if (routeData.Values.TryGetValue("action", out actionName))
            {
                var actionsFound = _actionNameMapping[actionName].OfType<ReflectedHttpActionDescriptor>().ToArray();

                // filter actions based on verb
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

                // if action name doesn't exists, found out actions based on verb directly
                log.HttpMethod = controllerContext.Request.Method;

                if (string.IsNullOrEmpty(actionName))
                {
                    actionsFoundByMethod = FindActionsForVerb(log.HttpMethod);

                    log.Mark(actionsFoundByMethod, info => info.FoundByVerb = true);
                }
            }

            // if none action is found at this stage a failure must happen
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
        /// A copy
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
        /// A copy
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
        /// A copy
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
        /// A copy
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
        /// A mimic
        /// 
        /// Issue: IActionMethodSelector is an internal interface. CacheAttrsIActionMethodSelector is a internal
        ///        property, too. The only implementation of IActionMethodSelector is NonActionAttribute, so the
        ///        codes are converted to directly filter out method with that attribute.
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
                    // Following codes will never happen, because there is no implementation of IActionMethodSelector
                    // returns true so far. And IActionMethodSelector is an internal interface.

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
    /// A copy
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
    /// A copy
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