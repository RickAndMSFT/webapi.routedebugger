Route Debugger is designed to help you understand routing mechanism in ASP.NET WebAPI.

The routing is composed of three steps:
1. Select a route
2. Select a controller
3. Select a action

The Route Debugger instrument WebAPI components save the clues about decision making in each of the three steps. The saved information are returned in a HTTP response. This is called a debugging process. A debugging process will be only triggered when a request is sent to the service has header 'RouteInspecting' and the value of which is true.  The debugging process is implemented by replace three components, IHttpControllerSelector, IHttpActionSelector and IHttpActionInvoker with instrumented version.

Beside above components, a debugging page is added as area. Navigate to http://<Site>/RouteDebugger can enter the page. In the page, you can send out inspect request to the route you want to test and the page will visualize the inspect data.

Note:
1. Route debugging page helps you to visualize the inspect data, however it is not required. A fiddler can helps you trigger the debugging process as well.
2. The components replacing is done in AreaRegistration, which happens before WebApiConfig. So if you replace these components in WebApiConfig, you need to redo the instrument by calling RouteDebuggerConfig.Register.
