#| ****************************************************************************
 * Copyright (c) Llewellyn Pritchard. 2007,2008,2009
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. 
 * A copy of the license can be found in the License.html file at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 * ***************************************************************************|#

(library (ironscheme web)
  (export
    context
    http-method
    querystring
    querystring-keys
    form
    form-keys
    header
    request
    response
    redirect
    rewrite-path
    application-item
    application-item-set!
    context-item
    context-item-set!
    session
    session-set!
    user-agent
    url-decode
    url-encode
    html-decode
    html-encode
    map-path
    resolve-url
    forms-authentication-logout
    forms-authentication-login
    request-raw-url
    http-output-port
    error-add!
    error-clear!
    user-name
    user-in-role?
    user-authenticated?
    display-html)
  (import 
    (ironscheme)
    (ironscheme contracts)
    (ironscheme xml)
    (ironscheme web-utils)
    (ironscheme clr))

  (clr-reference "System.Web, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
  (clr-using System.Web)
  (clr-using System.Collections.Specialized)
  (clr-using System.Web.SessionState)
  
  (define (context)
    (clr-static-prop-get HttpContext Current))
    
  (define (request)
    (clr-prop-get HttpContext Request (context)))    
    
  (define (response)
    (clr-prop-get HttpContext Response (context)))     
  
  (define (http-method)
    (string->symbol (string-downcase (clr-prop-get HttpRequest HttpMethod (request)))))    
  
  (define (get-querystring)
    (clr-prop-get HttpRequest QueryString (request)))

  (define (get-form)
    (clr-prop-get HttpRequest Form (request)))
    
  (define (get-headers)
    (clr-prop-get HttpRequest Headers (request)))
    
  (define (error-add! e)
    (clr-call HttpContext AddError (context) e))    
    
  (define (error-clear!)
    (clr-call HttpContext ClearError (context)))    

  (define (get-user)
    (clr-prop-get HttpContext User (context)))    
    
  (define (user-identity)
    (clr-prop-get System.Security.Principal.IPrincipal Identity (get-user)))
    
  (define (user-name)
    (clr-prop-get System.Security.Principal.IIdentity Name (user-identity)))  
    
  (define/contract (user-in-role? role:string)
    (clr-call System.Security.Principal.IPrincipal IsInRole (get-user) role))

  (define (user-authenticated?)
    (clr-prop-get System.Security.Principal.IIdentity IsAuthenticated (user-identity)))  
    
  (define/contract (resolve-url vpath:string)
    (clr-call HttpResponse ApplyAppPathModifier (response) vpath))    
    
  (define (nv-helper instance key)
    (define k (clr-indexer-get NameValueCollection instance (clr-cast String key)))
    (if (null? k) #f
        k))       

  (define (querystring key)
    (nv-helper (get-querystring) key))

  (define (querystring-keys)
    (clr-prop-get NameValueCollection AllKeys (get-querystring)))    

  (define (form key)
    (nv-helper (get-form) (->string key)))
    
  (define (form-keys)
    (clr-prop-get NameValueCollection AllKeys (get-form)))    
    
  (define (header key)
    (nv-helper (get-headers) (->string key)))
    
  (define (get-session)
    (clr-prop-get HttpContext Session (context)))  
    
  (define (get-app)
    (clr-prop-get HttpContext Application (context)))       

  (define (session key)
    (define k (clr-indexer-get HttpSessionState (get-session) (clr-cast String (->string key))))
    (if (null? k) #f
        k))       
  
  (define (session-set! key value)
    (clr-indexer-set! HttpSessionState (get-session) (clr-cast String (->string key)) value)
    (void))
    
  (define (application-item key)
    (define k (clr-indexer-get HttpApplicationState (get-app) (clr-cast String (->string key))))
    (if (null? k) #f
        k))       
  
  (define (application-item-set! key value)
    (clr-indexer-set! HttpApplicationState (get-app) (clr-cast String (->string key)) value)
    (void))    
    
  (define (items)
    (clr-prop-get HttpContext Items (context)))       
    
  (define (->string s)
    (cond 
      [(symbol? s) (symbol->string s)]
      [(string? s) s]
      [else 
        (assertion-violation '->string "not a symbol or string" s)]))
    
  (define (context-item key)
    (hashtable-ref (items) (->string key) #f))
  
  (define (context-item-set! key value)
    (hashtable-set! (items) (->string key) value))
    
  (define (user-agent)
    (clr-prop-get HttpRequest UserAgent (request)))
    
  (define (server-util)
    (clr-prop-get HttpContext Server (context)))
    
  (define/contract (map-path p:string)
    (clr-call HttpServerUtility MapPath (server-util) p))   
    
  (define (http-output-port)
    (clr-prop-get HttpResponse Output (response)))
    
  (define/contract (rewrite-path path:string)
    (clr-call HttpContext RewritePath (context) path))    
    
  (define redirect 
    (case/contract 
      [(path:string)               
        (clr-call HttpResponse Redirect (response) path)]    
      [(path:string endresponse?:boolean)  
        (clr-call HttpResponse Redirect (response) path endresponse?)]))
    
  (define (request-raw-url)
    (clr-prop-get HttpRequest RawUrl (request)))       
    
  (define (forms-authentication-logout)
    (clr-static-call System.Web.Security.FormsAuthentication SignOut))
    
  (define/contract (forms-authentication-login user:string)
    (clr-static-call System.Web.Security.FormsAuthentication SetAuthCookie user #f))      
  
  (define (display-html html)
    (display (->xml html) (http-output-port)))
    
)
