# Features
The Microsoft FHIR Server for Azure is an implementation of the [FHIR](https://hl7.org/fhir) standard. This document lists the main features of the FHIR Server. For a list of features that are planned or in development, see the [feature roadmap](Roadmap.md).

## FHIR Version
Current version: `3.0.1`

## REST API

| API                            | Supported | Comment | 
|--------------------------------|-----------|---------|
| read                           | Yes       |         |
| vread                          | Yes       |         |
| update                         | Yes       |         |
| update with optimistic locking | Yes	     |         |
| update (conditional)           | No        |	       |
| patch                          | No        |         |	
| delete                         | Yes       |         |
| delete (conditional)           | No        |         |	
| create                         | Yes       | Support both POST/PUT |
| create (conditional)           | No        |         |
| search                         | Partial   | See below |
| capabilities                   | Yes       |         | 
| batch                          | No        |         |
| transaction                    | No        |         |
| history                        | Yes       |         |
| paging                         | Partial   | `self` and `next` are supported |
| intermediaries                 | No        |         |


## Search

All search parameter types are supported. Chained parameters and reverse chaining are *not* supported. 

| Search parameter type | Supported | Comment |
|-----------------------|-----------|---------|
| Number                | Yes       |         |
| Date/DateTime	        | Yes       |         |
| String                | Yes       |         |
| Token                 | Yes       |         |
| Reference             | Yes       |         |
| Composite             | Yes       |         |
| Quantity              | Yes       | Issue [#103](https://github.com/Microsoft/fhir-server/issues/103) |
| URI                   | Yes       |         |


| Modifiers             | Supported	| Comment |
|-----------------------|-----------|---------|
|`:missing`             | Yes	    |         |
|`:exact`               | Yes       |         |
|`:contains`            | Yes       |         |
|`:text`                | Yes       |         |
|`:in` (token)          | No        |         |
|`:below` (token)       | No        |         |
|`:above` (token)       | No        |         |
|`:not-in` (token)      | No        |         |
|`:[type]` (reference)  | No        |         |
|`:below` (uri)         | Yes       |         |
|`:above` (uri)         | No        | Issue [#158](https://github.com/Microsoft/fhir-server/issues/158) |

| Common search parameter | Supported | Comment |
|-------------------------| ----------|---------|
| `_id`                   | Yes       |         |
| `_lastUpdated`          | Yes       |         |
| `_tag`                  | Yes       |         |
| `_profile`              | Yes       |         |
| `_security`             | Yes       |         |
| `_text`                 | No        |         |
| `_content`              | No        |         |
| `_list`                 | No        |         |
| `_has`                  | No        |         |
| `_type`                 | Yes       |         |
| `_query`                | No        |         |

| Search operations       | Supported | Comment |
|-------------------------|-----------|---------|
| `_filter`               | No        |         |
| `_sort`                 | No        |         |
| `_score`                | No        |         |
| `_count`                | Yes       |         |
| `_summary`              | Partial   | `_summary=count` is supported |
| `_include`              | No        |         |
| `_revinclude`           | No        |         |
| `_contained`            | No        |         |
| `_elements`             | No        |         |

## Persistence
The Microsoft FHIR Server has a pluggable persistence module (see [`Microsoft.Health.Fhir.Core.Features.Persistence`](../src/Microsoft.Health.Fhir.Core/Features/Persistence)). 

Currently the FHIR Server source code includes an implementation for [Azure Cosmos DB](https://docs.microsoft.com/en-us/azure/cosmos-db/). 

Cosmos DB is a globally distributed multi-model (SQL API, MongoDB API, etc.) database. It supports different [consistency levels](https://docs.microsoft.com/en-us/azure/cosmos-db/consistency-levels). The default deployment template configures the FHIR Server with `Strong` consistency, but the consistency policy can be modified (generally relaxed) on a request by request basis using the `x-ms-consistency-level` request header.

## Role Based Access Control 
The FHIR Server uses [Azure Active Directory](https://azure.microsoft.com/en-us/services/active-directory/) for [access control](Authentication.md). Specifically, Role Based Access Control (RBAC) is enforced, if the `FhirServer:Security:Enabled` configuration parameter is set to `true`, and all requests (except `/metadata`) to the FHIR Server must have `Authorization` request header set to `Bearer <TOKEN>`. The token must contain one or more roles as defined in the `roles` claim. A request will be allowed if the token contains a role that allows the specified action on the specified resource. 

Currently, the allowed actions for a given role are applied *globally* on the API. [Future work](Roadmap.md) is aimed at allowing more granular access based on a set of policies and filters.  
