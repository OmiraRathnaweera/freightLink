# ZAP Scanning Report

ZAP by [Checkmarx](https://checkmarx.com/).


## Summary of Alerts

| Risk Level | Number of Alerts |
| --- | --- |
| High | 0 |
| Medium | 0 |
| Low | 5 |
| Informational | 4 |




## Insights

| Level | Reason | Site | Description | Statistic |
| --- | --- | --- | --- | --- |
| Low | Exceeded High | http://host.docker.internal:5188 | Percentage of responses with status code 4xx | 98 % |
| Info | Informational |  | Percentage of network failures | 4 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of responses with status code 2xx | 5 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of responses with status code 5xx | 1 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with content type application/json | 26 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with method DELETE | 3 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with method GET | 48 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with method PATCH | 10 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with method POST | 32 % |
| Info | Informational | http://host.docker.internal:5188 | Percentage of endpoints with method PUT | 4 % |
| Info | Informational | http://host.docker.internal:5188 | Count of total endpoints | 417    |







## Alerts

| Name | Risk Level | Number of Instances |
| --- | --- | --- |
| A Server Error response code was returned by the server | Low | 10 |
| Application Error Disclosure | Low | 3 |
| Cross-Origin-Resource-Policy Header Missing or Invalid | Low | Systemic |
| Timestamp Disclosure - Unix | Low | 1 |
| Unexpected Content-Type was returned | Low | 2 |
| A Client Error response code was returned by the server | Informational | 403 |
| Authentication Request Identified | Informational | 1 |
| Information Disclosure - Sensitive Information in URL | Informational | 1 |
| Non-Storable Content | Informational | Systemic |




## Alert Detail



### [ A Server Error response code was returned by the server ](https://www.zaproxy.org/docs/alerts/100000/)



##### Low (High)

### Description

A response code of 500 was returned by the server.
This may indicate that the application is failing to handle unexpected input correctly.
Raised by the 'Alert on HTTP Response Code Error' script

* URL: http://host.docker.internal:5188/api/v1/files/publicId
  * Node Name: `http://host.docker.internal:5188/api/v1/files/publicId`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/publicId/
  * Node Name: `http://host.docker.internal:5188/api/v1/files/publicId/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content/
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content/3573803705549419192
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content/3573803705549419192`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content/publicId
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content/publicId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content/publicId/
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content/publicId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/single
  * Node Name: `http://host.docker.internal:5188/api/v1/files/single ()(multipart:file)`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/single
  * Node Name: `http://host.docker.internal:5188/api/v1/files/single ()(multipart:rtobject)`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/single/
  * Node Name: `http://host.docker.internal:5188/api/v1/files/single/ ()(multipart:file)`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `500`
  * Other Info: ``


Instances: 10

### Solution



### Reference



#### CWE Id: [ 388 ](https://cwe.mitre.org/data/definitions/388.html)


#### WASC Id: 20

#### Source ID: 4

### [ Application Error Disclosure ](https://www.zaproxy.org/docs/alerts/90022/)



##### Low (Medium)

### Description

This page contains an error/warning message that may disclose sensitive information like the location of the file that produced the unhandled exception. This information can be used to launch further attacks against the web application. The alert could be a false positive if the error message is found inside a documentation page.

* URL: http://host.docker.internal:5188/api/v1/files/publicId
  * Node Name: `http://host.docker.internal:5188/api/v1/files/publicId`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `HTTP/1.1 500 Internal Server Error`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/content/publicId
  * Node Name: `http://host.docker.internal:5188/api/v1/files/content/publicId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `HTTP/1.1 500 Internal Server Error`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/single
  * Node Name: `http://host.docker.internal:5188/api/v1/files/single ()(multipart:file)`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `HTTP/1.1 500 Internal Server Error`
  * Other Info: ``


Instances: 3

### Solution

Review the source code of this page. Implement custom error pages. Consider implementing a mechanism to provide a unique error reference/identifier to the client (browser) while logging the details on the server side and not exposing them to the user.

### Reference



#### CWE Id: [ 550 ](https://cwe.mitre.org/data/definitions/550.html)


#### WASC Id: 13

#### Source ID: 3

### [ Cross-Origin-Resource-Policy Header Missing or Invalid ](https://www.zaproxy.org/docs/alerts/90004/)



##### Low (Medium)

### Description

Cross-Origin-Resource-Policy header is an opt-in header designed to counter side-channels attacks like Spectre. Resource should be specifically set as shareable amongst different origins.

* URL: http://host.docker.internal:5188/api/v1/auth/agencies
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/agencies`
  * Method: `GET`
  * Parameter: `Cross-Origin-Resource-Policy`
  * Attack: ``
  * Evidence: ``
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/me
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/me`
  * Method: `GET`
  * Parameter: `Cross-Origin-Resource-Policy`
  * Attack: ``
  * Evidence: ``
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/v1/swagger.json
  * Node Name: `http://host.docker.internal:5188/swagger/v1/swagger.json`
  * Method: `GET`
  * Parameter: `Cross-Origin-Resource-Policy`
  * Attack: ``
  * Evidence: ``
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/forgot-password
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/forgot-password ()({email})`
  * Method: `POST`
  * Parameter: `Cross-Origin-Resource-Policy`
  * Attack: ``
  * Evidence: ``
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/resend-verification
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/resend-verification ()({email})`
  * Method: `POST`
  * Parameter: `Cross-Origin-Resource-Policy`
  * Attack: ``
  * Evidence: ``
  * Other Info: ``

Instances: Systemic


### Solution

Ensure that the application/web server sets the Cross-Origin-Resource-Policy header appropriately, and that it sets the Cross-Origin-Resource-Policy header to 'same-origin' for all web pages.
'same-site' is considered as less secured and should be avoided.
If resources must be shared, set the header to 'cross-origin'.
If possible, ensure that the end user uses a standards-compliant and modern web browser that supports the Cross-Origin-Resource-Policy header (https://caniuse.com/mdn-http_headers_cross-origin-resource-policy).

### Reference


* [ https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Cross-Origin-Embedder-Policy ](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Cross-Origin-Embedder-Policy)


#### CWE Id: [ 693 ](https://cwe.mitre.org/data/definitions/693.html)


#### WASC Id: 14

#### Source ID: 3

### [ Timestamp Disclosure - Unix ](https://www.zaproxy.org/docs/alerts/10096/)



##### Low (Low)

### Description

A timestamp was disclosed by the application/web server. - Unix

* URL: http://host.docker.internal:5188/api/v1/auth/me
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/me`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `1791001816`
  * Other Info: `1791001816, which evaluates to: 2026-10-03 04:30:16.`


Instances: 1

### Solution

Manually confirm that the timestamp data is not sensitive, and that the data cannot be aggregated to disclose exploitable patterns.

### Reference


* [ https://cwe.mitre.org/data/definitions/200.html ](https://cwe.mitre.org/data/definitions/200.html)


#### CWE Id: [ 497 ](https://cwe.mitre.org/data/definitions/497.html)


#### WASC Id: 13

#### Source ID: 3

### [ Unexpected Content-Type was returned ](https://www.zaproxy.org/docs/alerts/100001/)



##### Low (High)

### Description

A Content-Type of text/html was returned by the server.
This is not one of the types expected to be returned by an API.
Raised by the 'Alert on Unexpected Content Types' script

* URL: http://host.docker.internal:5188/swagger/
  * Node Name: `http://host.docker.internal:5188/swagger/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `text/html`
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/index.html
  * Node Name: `http://host.docker.internal:5188/swagger/index.html`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `text/html`
  * Other Info: ``


Instances: 2

### Solution



### Reference




#### Source ID: 4

### [ A Client Error response code was returned by the server ](https://www.zaproxy.org/docs/alerts/100000/)



##### Informational (High)

### Description

A response code of 404 was returned by the server.
This may indicate that the application is failing to handle unexpected input correctly.
Raised by the 'Alert on HTTP Response Code Error' script

* URL: http://host.docker.internal:5188/api/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/invoices/id ()({voidReason})`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/ ()({voidReason})`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/id
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/id`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/id/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/id
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/id`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/id/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id ()({voidReason})`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/ ()({voidReason})`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files/fileId
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files/fileId`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files/fileId/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files/fileId/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188
  * Node Name: `http://host.docker.internal:5188`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/
  * Node Name: `http://host.docker.internal:5188/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/5915770773825691710
  * Node Name: `http://host.docker.internal:5188/5915770773825691710`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api
  * Node Name: `http://host.docker.internal:5188/api`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/
  * Node Name: `http://host.docker.internal:5188/api/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/6392330707868380105
  * Node Name: `http://host.docker.internal:5188/api/6392330707868380105`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes%3FPage=10&PageSize=10&Status=Raised&Category=Damage&TripId=TripId&SortOrder=SortOrder
  * Node Name: `http://host.docker.internal:5188/api/disputes (Category,Page,PageSize,SortOrder,Status,TripId)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/7426691963177025710
  * Node Name: `http://host.docker.internal:5188/api/disputes/7426691963177025710`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id
  * Node Name: `http://host.docker.internal:5188/api/disputes/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/7336341693074924557
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/7336341693074924557`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/actuator/health
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/actuator/health`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices%3FPage=10&PageSize=10&Status=Draft&TripId=TripId&RecipientId=RecipientId&StartDate=StartDate&EndDate=EndDate&Search=ZAP&SortOrder=SortOrder
  * Node Name: `http://host.docker.internal:5188/api/invoices (EndDate,Page,PageSize,RecipientId,Search,SortOrder,StartDate,Status,TripId)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/1416559210465374353
  * Node Name: `http://host.docker.internal:5188/api/invoices/1416559210465374353`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/delivery-event
  * Node Name: `http://host.docker.internal:5188/api/invoices/delivery-event`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/delivery-event/
  * Node Name: `http://host.docker.internal:5188/api/invoices/delivery-event/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/delivery-event/5027785071902279652
  * Node Name: `http://host.docker.internal:5188/api/invoices/delivery-event/5027785071902279652`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/invoices/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/1008782566611982746
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/1008782566611982746`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/on-delivery
  * Node Name: `http://host.docker.internal:5188/api/invoices/on-delivery`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/on-delivery/
  * Node Name: `http://host.docker.internal:5188/api/invoices/on-delivery/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/on-delivery/3154998787051147905
  * Node Name: `http://host.docker.internal:5188/api/invoices/on-delivery/3154998787051147905`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/recipients
  * Node Name: `http://host.docker.internal:5188/api/invoices/recipients`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/recipients/
  * Node Name: `http://host.docker.internal:5188/api/invoices/recipients/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/summary
  * Node Name: `http://host.docker.internal:5188/api/invoices/summary`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/summary/
  * Node Name: `http://host.docker.internal:5188/api/invoices/summary/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1
  * Node Name: `http://host.docker.internal:5188/api/v1`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/
  * Node Name: `http://host.docker.internal:5188/api/v1/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/1040287228876112544
  * Node Name: `http://host.docker.internal:5188/api/v1/1040287228876112544`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin
  * Node Name: `http://host.docker.internal:5188/api/v1/admin`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/6607365369885516264
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/6607365369885516264`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/6446871392787747500
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/6446871392787747500`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/5461773534958936427
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/5461773534958936427`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics/433771611538669618
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics/433771611538669618`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics/summary
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics/summary`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics/summary/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics/summary/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/7497027316162491343
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/7497027316162491343`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/5928055874197491558
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/5928055874197491558`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/history
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/history`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/history/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/history/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/6653133526733924414
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/6653133526733924414`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history%3FfuelType=AutoDiesel
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history (fuelType)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/5098072416110569263
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/5098072416110569263`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/history%3FvehicleClass=MiniTruck
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/history (vehicleClass)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/history/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/history/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies%3FPage=10&PageSize=10&SortBy=SortBy&SortDir=SortDir&Search=ZAP&Status=Pending
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies (Page,PageSize,Search,SortBy,SortDir,Status)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/3745211044952765965
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/3745211044952765965`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/expiring-compliance%3Fdays=30
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/expiring-compliance (days)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/expiring-compliance/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/expiring-compliance/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/2189029925854497100
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/2189029925854497100`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/7974592160141362888
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/7974592160141362888`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/1249140525866273204
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/1249140525866273204`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/2131024598367658831
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/2131024598367658831`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/7477262808719245412
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/7477262808719245412`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/fleet
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/fleet`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/fleet/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/fleet/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/5599868387516355762
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/5599868387516355762`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/8568217173220753096
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/8568217173220753096`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/my
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/my`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/my/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/my/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/my/6220830945453988938
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/my/6220830945453988938`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/my/fleet%3FagencyId=agencyId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/my/fleet (agencyId)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/my/fleet/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/my/fleet/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/summary
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/summary`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/summary/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/summary/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/verification-queue
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/verification-queue`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/verification-queue/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/verification-queue/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignment-actions
  * Node Name: `http://host.docker.internal:5188/api/v1/assignment-actions`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignment-actions/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignment-actions/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignment-actions/9163311789685514106
  * Node Name: `http://host.docker.internal:5188/api/v1/assignment-actions/9163311789685514106`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments%3FPage=10&PageSize=10&Status=Proposed&Search=ZAP&HasTrip=true&SortBy=SortBy&SortDir=SortDir
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments (HasTrip,Page,PageSize,Search,SortBy,SortDir,Status)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/5664500772671667966
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/5664500772671667966`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/id
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/id/4837416978738312554
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/id/4837416978738312554`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/584042255051144598
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/584042255051144598`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth
  * Node Name: `http://host.docker.internal:5188/api/v1/auth`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/1845468062267619293
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/1845468062267619293`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/5926813144130788966
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/5926813144130788966`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes%3FPage=10&PageSize=10&Status=Raised&Category=Damage&TripId=TripId&SortOrder=SortOrder
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes (Category,Page,PageSize,SortOrder,Status,TripId)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/972791363685860990
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/972791363685860990`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/9220917280634756045
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/9220917280634756045`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files
  * Node Name: `http://host.docker.internal:5188/api/v1/files`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/
  * Node Name: `http://host.docker.internal:5188/api/v1/files/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/files/7787767114608329877
  * Node Name: `http://host.docker.internal:5188/api/v1/files/7787767114608329877`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices%3FPage=10&PageSize=10&Status=Draft&TripId=TripId&RecipientId=RecipientId&StartDate=StartDate&EndDate=EndDate&Search=ZAP&SortOrder=SortOrder
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices (EndDate,Page,PageSize,RecipientId,Search,SortOrder,StartDate,Status,TripId)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/3354235362135206828
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/3354235362135206828`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/delivery-event
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/delivery-event`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/delivery-event/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/delivery-event/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/delivery-event/1136894711733199185
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/delivery-event/1136894711733199185`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/5032532619534569121
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/5032532619534569121`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/on-delivery
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/on-delivery`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/on-delivery/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/on-delivery/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/on-delivery/3659033463031675273
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/on-delivery/3659033463031675273`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/recipients
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/recipients`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/recipients/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/recipients/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/summary
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/summary`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/summary/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/summary/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads%3FPage=10&PageSize=10&SortBy=SortBy&SortDir=SortDir&Search=ZAP&Status=Draft&ShipperUserId=ShipperUserId&CreatedFrom=CreatedFrom&CreatedTo=CreatedTo&Marketplace=true
  * Node Name: `http://host.docker.internal:5188/api/v1/loads (CreatedFrom,CreatedTo,Marketplace,Page,PageSize,Search,ShipperUserId,SortBy,SortDir,Status)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/8807297916642709619
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/8807297916642709619`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/3035523928504805143
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/3035523928504805143`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/7708751657333710352
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/7708751657333710352`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files/3932613048004043851
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files/3932613048004043851`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/1879115560639360530
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/1879115560639360530`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/history
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/history`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/history/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/history/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/8818978682061650067
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/8818978682061650067`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/5551776443695739007
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/5551776443695739007`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips
  * Node Name: `http://host.docker.internal:5188/api/v1/trips`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips%3FPage=10&PageSize=10&SortBy=SortBy&SortDir=SortDir&Status=Assigned&AgencyId=AgencyId&DriverId=DriverId&HasInvoice=true
  * Node Name: `http://host.docker.internal:5188/api/v1/trips (AgencyId,DriverId,HasInvoice,Page,PageSize,SortBy,SortDir,Status)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/1512816179502471302
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/1512816179502471302`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/5091922009203568754
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/5091922009203568754`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/evidence
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/evidence`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/evidence/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/evidence/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal
  * Node Name: `http://host.docker.internal:5188/internal`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/
  * Node Name: `http://host.docker.internal:5188/internal/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/2203468246545195571
  * Node Name: `http://host.docker.internal:5188/internal/2203468246545195571`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `405`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/6159381328720972463
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/6159381328720972463`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/8411506529203031080
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/8411506529203031080`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/pricing
  * Node Name: `http://host.docker.internal:5188/internal/pricing`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/pricing/
  * Node Name: `http://host.docker.internal:5188/internal/pricing/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/pricing/5327145620933226991
  * Node Name: `http://host.docker.internal:5188/internal/pricing/5327145620933226991`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/routing
  * Node Name: `http://host.docker.internal:5188/internal/routing`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/routing/
  * Node Name: `http://host.docker.internal:5188/internal/routing/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/routing/688315644126265932
  * Node Name: `http://host.docker.internal:5188/internal/routing/688315644126265932`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/7115565931287022878
  * Node Name: `http://host.docker.internal:5188/swagger/7115565931287022878`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/v1
  * Node Name: `http://host.docker.internal:5188/swagger/v1`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/v1/
  * Node Name: `http://host.docker.internal:5188/swagger/v1/`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/swagger/v1/4923274423259759928
  * Node Name: `http://host.docker.internal:5188/swagger/v1/4923274423259759928`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/resolve
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/resolve ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/resolve/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/resolve/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/review
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/review`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/review/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/review/`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/status
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/status ()({status,resolutionNote,notes,outcome})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/status/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/status/ ()({status,resolutionNote,notes,outcome})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/cancel
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/cancel ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/cancel/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/cancel/ ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/status
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/status ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/status/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/status/ ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/void
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/void ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/void/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/void/ ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/status
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/status ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/status/ ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/status
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/status ()({status,reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/status/ ()({status,reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/status
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/status ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/status/ ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/me
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/me ()({fullName,email,phoneE164})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/me/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/me/ ()({fullName,email,phoneE164})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/resolve
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/resolve ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/resolve/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/resolve/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/review
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/review`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/review/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/review/`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/status
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/status ()({status,resolutionNote,notes,outcome})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/status/ ()({status,resolutionNote,notes,outcome})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/cancel
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/cancel ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/cancel/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/cancel/ ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/status
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/status ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/status/ ()({status})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/void
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/void ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/void/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/void/ ()({voidReason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/status
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/status ()({status,reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/status/ ()({status,reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/cancel
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/cancel ()({reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/cancel/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/cancel/ ()({reason})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/computeMetadata/v1/
  * Node Name: `http://host.docker.internal:5188/computeMetadata/v1/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/latest/meta-data/
  * Node Name: `http://host.docker.internal:5188/latest/meta-data/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/metadata/instance
  * Node Name: `http://host.docker.internal:5188/metadata/instance ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/metadata/v1
  * Node Name: `http://host.docker.internal:5188/metadata/v1 ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/opc/v1/instance/
  * Node Name: `http://host.docker.internal:5188/opc/v1/instance/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/opc/v2/instance/
  * Node Name: `http://host.docker.internal:5188/opc/v2/instance/ ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/openstack/latest/meta_data.json
  * Node Name: `http://host.docker.internal:5188/openstack/latest/meta_data.json ()({outcome,resolutionNote,notes})`
  * Method: `PATCH`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes
  * Node Name: `http://host.docker.internal:5188/api/disputes ()({tripId,category,description})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/
  * Node Name: `http://host.docker.internal:5188/api/disputes/ ()({tripId,category,description})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/resolve
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/resolve ()({outcome,resolutionNote,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/resolve/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/resolve/ ()({outcome,resolutionNote,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices
  * Node Name: `http://host.docker.internal:5188/api/invoices ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes,status,issueImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/
  * Node Name: `http://host.docker.internal:5188/api/invoices/ ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes,status,issueImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/delivery-event/tripId
  * Node Name: `http://host.docker.internal:5188/api/invoices/delivery-event/tripId`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/delivery-event/tripId/
  * Node Name: `http://host.docker.internal:5188/api/invoices/delivery-event/tripId/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/confirm-payment
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/confirm-payment`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/confirm-payment/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/confirm-payment/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/issue
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/issue`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/issue/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/issue/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/payment-proof
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/payment-proof ()({publicId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/payment-proof/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/payment-proof/ ()({publicId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/void
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/void ()({voidReason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/void/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/void/ ()({voidReason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/on-delivery/tripId
  * Node Name: `http://host.docker.internal:5188/api/invoices/on-delivery/tripId`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/on-delivery/tripId/
  * Node Name: `http://host.docker.internal:5188/api/invoices/on-delivery/tripId/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/approve
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/approve ()({agencyId,vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/approve/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/agent-workflows/workflowRunId/approve/ ()({agencyId,vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config ()({baseFare,ratePerKg,driverCostPerKm,maintenanceAllowancePerKm,marginPercent,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/formula-config/ ()({baseFare,ratePerKg,driverCostPerKm,maintenanceAllowancePerKm,marginPercent,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates ()({fuelType,pricePerLitre,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/ ()({fuelType,pricePerLitre,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency ()({classLabel,minPayloadKg,maxPayloadKg,minVolumeM3,maxVolumeM3,fuelConsumptionLPer100Km,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/vehicle-efficiency/ ()({classLabel,minPayloadKg,maxPayloadKg,minVolumeM3,maxVolumeM3,fuelConsumptionLPer100Km,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies ()({name,businessRegNo,yardAddress,yardLat,yardLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/ ()({name,businessRegNo,yardAddress,yardLat,yardLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/activate
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/activate`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/activate/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/activate/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs ()({publicId,docType,docNumber,issuedOn,expiresOn})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/ ()({publicId,docType,docNumber,issuedOn,expiresOn})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/reject
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/reject`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/reject/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/reject/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/verify
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/verify`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/verify/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/verify/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers ()({email,fullName,phoneE164,licenceNo,licenceExpiry})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/ ()({email,fullName,phoneE164,licenceNo,licenceExpiry})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/suspend
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/suspend`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/suspend/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/suspend/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles ()({registrationNo,vehicleType,capacityKg,volumeM3})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/ ()({registrationNo,vehicleType,capacityKg,volumeM3})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/verify
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/verify`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/verify/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/verify/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/seed-defaults
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/seed-defaults`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/seed-defaults/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/seed-defaults/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignment-actions/respond
  * Node Name: `http://host.docker.internal:5188/api/v1/assignment-actions/respond ()({token})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignment-actions/respond/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignment-actions/respond/ ()({token})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/id/approve
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/id/approve ()({vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/id/approve/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/id/approve/ ()({vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/accept
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/accept ()({vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/accept/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/accept/ ()({vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/decline
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/decline ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/assignments/loadId/decline/
  * Node Name: `http://host.docker.internal:5188/api/v1/assignments/loadId/decline/ ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/change-password
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/change-password ()({currentPassword,newPassword})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/change-password
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/change-password ()({currentPassword,newPassword})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/change-password/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/change-password/ ()({currentPassword,newPassword})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/forgot-password
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/forgot-password ()({email})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/login
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/login ()({email,password})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/login
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/login ()({email,password})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/login/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/login/ ()({email,password})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/logout
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/logout ()({refreshToken})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/logout/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/logout/ ()({refreshToken})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/refresh
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/refresh ()({refreshToken})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/refresh/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/refresh/ ()({refreshToken})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/agency
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/agency ()({email,password,fullName,phoneE164,jobTitle,agencyName,businessRegNo,yardAddress,yardLat,yardLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/agency/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/agency/ ()({email,password,fullName,phoneE164,jobTitle,agencyName,businessRegNo,yardAddress,yardLat,yardLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/shipper
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/shipper ()({email,password,fullName,phoneE164,companyName,businessRegNo,billingAddress})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/register/shipper/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/register/shipper/ ()({email,password,fullName,phoneE164,companyName,businessRegNo,billingAddress})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/resend-verification
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/resend-verification ()({email})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/reset-password
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/reset-password ()({token,newPassword})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/reset-password/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/reset-password/ ()({token,newPassword})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/verify-email
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/verify-email ()({token})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/auth/verify-email/
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/verify-email/ ()({token})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes ()({tripId,category,description})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/ ()({tripId,category,description})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/resolve
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/resolve ()({outcome,resolutionNote,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/resolve/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/resolve/ ()({outcome,resolutionNote,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes,status,issueImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/ ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes,status,issueImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/delivery-event/tripId
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/delivery-event/tripId`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/delivery-event/tripId/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/delivery-event/tripId/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/confirm-payment
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/confirm-payment`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/confirm-payment/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/confirm-payment/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/issue
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/issue`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/issue/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/issue/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/payment-proof
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/payment-proof ()({publicId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/payment-proof/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/payment-proof/ ()({publicId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/void
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/void ()({voidReason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/void/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/void/ ()({voidReason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/on-delivery/tripId
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/on-delivery/tripId`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/on-delivery/tripId/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/on-delivery/tripId/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads
  * Node Name: `http://host.docker.internal:5188/api/v1/loads ()({cargoDescription,weightKg,volumeM3,pickupAddress,pickupLat,pickupLng,dropoffAddress,dropoffLat,dropoffLng,pickupWindowStart,pickupWindowEnd,postImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/ ()({cargoDescription,weightKg,volumeM3,pickupAddress,pickupLat,pickupLng,dropoffAddress,dropoffLat,dropoffLng,pickupWindowStart,pickupWindowEnd,postImmediately})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `400`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/estimate
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/estimate`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/estimate/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/estimate/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files ()({publicId,fileType})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/files/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/files/ ()({publicId,fileType})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/confirm
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/confirm ()({agencyId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/confirm/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/confirm/ ()({agencyId})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/reject
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/reject ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/reject/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/reject/ ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/revise
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/revise ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/revise/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/revise/ ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/trigger
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/trigger`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/match/trigger/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/match/trigger/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals ()({proposedPrice,message})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/ ()({proposedPrice,message})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/accept
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/accept`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/accept/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/accept/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/reject
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/reject ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/reject/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/reject/ ()({reason})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/withdraw
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/withdraw`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/withdraw/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/loadId/proposals/proposalId/withdraw/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips
  * Node Name: `http://host.docker.internal:5188/api/v1/trips ()({assignmentId,vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/ ()({assignmentId,vehicleId,driverId,notes})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/evidence
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/evidence ()({publicId,evidenceType,capturedLat,capturedLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/evidence/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/evidence/ ()({publicId,evidenceType,capturedLat,capturedLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/status
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/status ()({targetStatus,notes,snapshotLat,snapshotLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/status/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/status/ ()({targetStatus,notes,snapshotLat,snapshotLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/seed-example
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/seed-example`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/seed-example/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/seed-example/`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `403`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs ()({loadId,triggeredByUserId,attemptNo})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/ ()({loadId,triggeredByUserId,attemptNo})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/candidates
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/candidates ()([{agencyId,rank,eligible,eligibilityScore,rejectionReason}..])`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/candidates/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/candidates/ ()([{agencyId,rank,eligible,eligibilityScore,rejectionReason}..])`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/steps
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/steps ()({stepNo,agentRole,status,inputJson,outputJson,errorMessage,durationMs,startedAt,completedAt})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/steps/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/steps/ ()({stepNo,agentRole,status,inputJson,outputJson,errorMessage,durationMs,startedAt,completedAt})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/tool-calls
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/tool-calls ()({agentStepId,toolName,attemptNo,requestJson,responseJson,success,httpStatusCode,durationMs,errorMessage,calledAt})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/tool-calls/
  * Node Name: `http://host.docker.internal:5188/internal/agent-workflow-runs/workflowRunId/tool-calls/ ()({agentStepId,toolName,attemptNo,requestJson,responseJson,success,httpStatusCode,durationMs,errorMessage,calledAt})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/pricing/estimate
  * Node Name: `http://host.docker.internal:5188/internal/pricing/estimate ()({loadId,suggestedVehicleClass,distanceKm})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/pricing/estimate/
  * Node Name: `http://host.docker.internal:5188/internal/pricing/estimate/ ()({loadId,suggestedVehicleClass,distanceKm})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/routing/route-eta
  * Node Name: `http://host.docker.internal:5188/internal/routing/route-eta ()({originLat,originLng,destinationLat,destinationLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/internal/routing/route-eta/
  * Node Name: `http://host.docker.internal:5188/internal/routing/route-eta/ ()({originLat,originLng,destinationLat,destinationLng})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `401`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id
  * Node Name: `http://host.docker.internal:5188/api/disputes/id ()({category,description})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/disputes/id/
  * Node Name: `http://host.docker.internal:5188/api/disputes/id/ ()({category,description})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/invoices/id ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/invoices/id/ ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id ()({name,yardAddress,yardLat,yardLng})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/ ()({name,yardAddress,yardLat,yardLng})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId ()({publicId,docNumber,issuedOn,expiresOn})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/compliance-docs/docId/ ()({publicId,docNumber,issuedOn,expiresOn})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId ()({fullName,licenceNo,licenceExpiry})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/drivers/driverId/ ()({fullName,licenceNo,licenceExpiry})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId ()({registrationNo,vehicleType,capacityKg,volumeM3})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/
  * Node Name: `http://host.docker.internal:5188/api/v1/agencies/id/vehicles/vehicleId/ ()({registrationNo,vehicleType,capacityKg,volumeM3})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id ()({category,description})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/disputes/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/disputes/id/ ()({category,description})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/invoices/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/invoices/id/ ()({tripId,linkedEntityId,recipientId,recipientRole,lineItems:[{invoiceLineItemId,description,quantity,unitPrice,taxRate,amount}],amount,discountTotal,currency,dueDate,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id ()({cargoDescription,weightKg,volumeM3,pickupAddress,pickupLat,pickupLng,dropoffAddress,dropoffLat,dropoffLng,pickupWindowStart,pickupWindowEnd})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/loads/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/loads/id/ ()({cargoDescription,weightKg,volumeM3,pickupAddress,pickupLat,pickupLng,dropoffAddress,dropoffLat,dropoffLng,pickupWindowStart,pickupWindowEnd})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id ()({vehicleId,driverId,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/trips/id/
  * Node Name: `http://host.docker.internal:5188/api/v1/trips/id/ ()({vehicleId,driverId,notes})`
  * Method: `PUT`
  * Parameter: ``
  * Attack: ``
  * Evidence: `404`
  * Other Info: ``


Instances: 403

### Solution



### Reference



#### CWE Id: [ 388 ](https://cwe.mitre.org/data/definitions/388.html)


#### WASC Id: 20

#### Source ID: 4

### [ Authentication Request Identified ](https://www.zaproxy.org/docs/alerts/10111/)



##### Informational (High)

### Description

The given request has been identified as an authentication request. The 'Other Info' field contains a set of key=value lines which identify any relevant fields. If the request is in a context which has an Authentication Method set to "Auto-Detect" then this rule will change the authentication to match the request identified.

* URL: http://host.docker.internal:5188/api/v1/auth/login
  * Node Name: `http://host.docker.internal:5188/api/v1/auth/login ()({email,password})`
  * Method: `POST`
  * Parameter: `email`
  * Attack: ``
  * Evidence: `password`
  * Other Info: `userParam=email
userValue=zaproxy@example.com
passwordParam=password`


Instances: 1

### Solution

This is an informational alert rather than a vulnerability and so there is nothing to fix.

### Reference


* [ https://www.zaproxy.org/docs/desktop/addons/authentication-helper/auth-req-id/ ](https://www.zaproxy.org/docs/desktop/addons/authentication-helper/auth-req-id/)



#### Source ID: 3

### [ Information Disclosure - Sensitive Information in URL ](https://www.zaproxy.org/docs/alerts/10024/)



##### Informational (Medium)

### Description

The request appeared to contain sensitive information leaked in the URL. This can violate PCI and most organizational compliance policies. You can configure the list of strings for this check to add or remove values specific to your environment.

* URL: http://host.docker.internal:5188/api/v1/loads%3FPage=10&PageSize=10&SortBy=SortBy&SortDir=SortDir&Search=ZAP&Status=Draft&ShipperUserId=ShipperUserId&CreatedFrom=CreatedFrom&CreatedTo=CreatedTo&Marketplace=true
  * Node Name: `http://host.docker.internal:5188/api/v1/loads (CreatedFrom,CreatedTo,Marketplace,Page,PageSize,Search,ShipperUserId,SortBy,SortDir,Status)`
  * Method: `GET`
  * Parameter: `ShipperUserId`
  * Attack: ``
  * Evidence: `ShipperUserId`
  * Other Info: `The URL contains potentially sensitive information. The following string was found via the pattern: user
ShipperUserId`


Instances: 1

### Solution

Do not pass sensitive information in URIs.

### Reference



#### CWE Id: [ 598 ](https://cwe.mitre.org/data/definitions/598.html)


#### WASC Id: 13

#### Source ID: 3

### [ Non-Storable Content ](https://www.zaproxy.org/docs/alerts/10049/)



##### Informational (Medium)

### Description

The response contents are not storable by caching components such as proxy servers. If the response does not contain sensitive, personal or user-specific information, it may benefit from being stored and cached, to improve performance.

* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/id`
  * Method: `DELETE`
  * Parameter: ``
  * Attack: ``
  * Evidence: `DELETE `
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/analytics/summary
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/analytics/summary`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `authorization:`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `authorization:`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history%3FfuelType=AutoDiesel
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates/history (fuelType)`
  * Method: `GET`
  * Parameter: ``
  * Attack: ``
  * Evidence: `authorization:`
  * Other Info: ``
* URL: http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates
  * Node Name: `http://host.docker.internal:5188/api/v1/admin/pricing/fuel-rates ()({fuelType,pricePerLitre,source,effectiveFrom})`
  * Method: `POST`
  * Parameter: ``
  * Attack: ``
  * Evidence: `authorization:`
  * Other Info: ``

Instances: Systemic


### Solution

The content may be marked as storable by ensuring that the following conditions are satisfied:
The request method must be understood by the cache and defined as being cacheable ("GET", "HEAD", and "POST" are currently defined as cacheable)
The response status code must be understood by the cache (one of the 1XX, 2XX, 3XX, 4XX, or 5XX response classes are generally understood)
The "no-store" cache directive must not appear in the request or response header fields
For caching by "shared" caches such as "proxy" caches, the "private" response directive must not appear in the response
For caching by "shared" caches such as "proxy" caches, the "Authorization" header field must not appear in the request, unless the response explicitly allows it (using one of the "must-revalidate", "public", or "s-maxage" Cache-Control response directives)
In addition to the conditions above, at least one of the following conditions must also be satisfied by the response:
It must contain an "Expires" header field
It must contain a "max-age" response directive
For "shared" caches such as "proxy" caches, it must contain a "s-maxage" response directive
It must contain a "Cache Control Extension" that allows it to be cached
It must have a status code that is defined as cacheable by default (200, 203, 204, 206, 300, 301, 404, 405, 410, 414, 501).

### Reference


* [ https://datatracker.ietf.org/doc/html/rfc7234 ](https://datatracker.ietf.org/doc/html/rfc7234)
* [ https://datatracker.ietf.org/doc/html/rfc7231 ](https://datatracker.ietf.org/doc/html/rfc7231)
* [ https://www.w3.org/Protocols/rfc2616/rfc2616-sec13.html ](https://www.w3.org/Protocols/rfc2616/rfc2616-sec13.html)


#### CWE Id: [ 524 ](https://cwe.mitre.org/data/definitions/524.html)


#### WASC Id: 13

#### Source ID: 3


