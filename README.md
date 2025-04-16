

# Pagin8

Pagin8 is a C# library that provides a simple way to generate SQL queries. It allows you to build complex queries with ease and provides a fluent interface that is easy to read and understand. The filtering syntax is inspired by PostgREST server, and supports a wide range of operators for comparing values and constructing complex expressions.

## Querying Syntax

To filter the results of a GET request, add a filter query parameter to the URL, followed by a string that defines the filtering conditions. The basic syntax for a filter is:

```
<column>=<operator>.<value>
```
and inside grouping operators:
```
<column>.<operator>.<value>
```

where `<column>` is the name of the column you want to filter on, `<operator>` is one of the supported operators, and `<value>` is the value you want to compare against.

To filter the data on specific endpoint, use URL query parameters. For example, to retrieve all customers with the name `John`, you can make a GET request to:

```
http://<customer_filter_endpoint>?name=eq.John
```

## Complex Expressions

You can construct complex filtering expressions by combining multiple conditions using logical operators (and, or, not). For example, to filter the orders table by both the customer_name and order_date columns, you would use the following URL:

```
GET /<orders_filter_endpoint>?customer_name=eq.John&order_date=gt.2022-01-01
```

This would return all the orders where the customer_name is equal to `John` and the order_date is after `January 1st, 2022`.

You can also go further and apply more complex logic to the conditions:

```
GET /<student_filtering_endpoint>?grade=gte.6&student=is.true&or=(age.eq.20,not.and(age.lte.17,age.gte.19))
```

Multiple conditions on columns are evaluated using `AND` by default, but you can combine them using `OR` with the or operator. For example, query above will return students where:

* The student's `grade` is greater than or equal to 6 (grade >= 6)
* The student `status` is active (student = true)
* Either:
  * The student's `age` is equal to 20 (age = 20)
  * Or, the student's `age` is not less then or equal to 17 and greater then or equal to 19 (so 18)


## Supported Operators

Pagin8 support a wide range of operators for comparing values and constructing complex expressions. Here are the supported operators:

### Comparison Operators

-   `eq`: Equal to
-   `gt`: Greater than
-   `lt`: Less than
-   `gte`: Greater than or equal to
-   `lte`: Less than or equal to

### String Operators

-   `like`: Like
-   `cs`: Contains
-   `stw`: Starts With
-   `enw`: Ends With
-   `not.`: Logical NOT prefix for all operators ( `not.like` | `not.cs`... )

### Logical Operators

-   `and`: Logical AND
-   `or`: Logical OR
-   `not`: Logical NOT ( `not.and()` | `not.or()` )

### Array Operators

-   `in`: In
-   `not`: Logical NOT ( `not.in` )
-   `incl`: Includes all values
-   `excl`: Exclude all values

### Boolean Operators

-   `is`: Is (true | false), ($empty, not.$empty)
-   `not`: Logical NOT ( `not.is` )

### Date Range Operators

-   `ago`
-   `for`
-   `not`: Logical NOT ( `not.ago` | `not.for` )


## Vertical Filtering (Columns)

In some cases, certain tables in a dataset may contain wide columns with a large amount of data. To optimize server performance and improve response times, Pagin8 offers the ability to withhold these columns from the API response. The client can specify which columns are required using the `select` parameter. If select is not provided, default is `*`, meaning all columns will be returned. `*` token should not be provided in select as it is default one.

```
GET /<student_filtering_endpoint>?select=name,age
```
```json
[
  {"name": "John", "age": 20},
  {"name": "Jane", "age": 23}
]
```


## Nested Filtering Syntax

The `with` operator allows nested filtering based on complex types within the filtering context.

### Syntax

The syntax for using the `with` operator is as follows:

```
<complexField>.with=(<conditions>)
```

-   `<complexField>`: Specifies the complex type field that needs to be unfolded for filtering.
-   `<conditions>`: Specifies the filtering condition for the nested properties.

### Indicator for Complex Type

The presence of `with` indicates that the filtered property is a complex type containing nested properties.

One more indicator is column metadata which is returned after `metaInclude=columns` is used in a filter, in the following format:

```json
...
{
          "name": "userTags",
          "type": "object",
          "flags": [
            "no-sort",
            "array"
          ],
          "properties": [
            {
              "name": "colorCode",
              "type": "string"
            },
            {
              "name": "description",
              "type": "string"
            },
            {
              "name": "extraData",
              "type": "string"
            },
            {
              "name": "id",
              "type": "int"
            },
            {
              "name": "name",
              "type": "string"
            }
          ]
        },
        {
          "name": "versionId",
          "type": "int"
        }
        ...
```
In the case of complex type, `properties` information is included in the response, so the client is aware of inner operations applicable to nested properties.

The `type` attribute specifies whether the field is a primitive type like an integer or a complex object. The presence of `array` in `flags` signifies that the field is an array of objects, providing detailed information through nested properties. Omitting `array` in flags and keeping `type: object`, will indicate that it is just a single nested object.

Example:
  - `type:object` + `flags: [array]` = **array of complex objects** (`GetTagInfo[]` in case of `userTags`)
  - `type:object` + `flags: []` = **single complex object** (`Contact` i.e.`)



### Unwrapping and Inner Conditions

When `with` is used, it unwraps the complex type (`userTags`) and applies the specified conditions to filter its inner properties.

### Supported Operators

Currently, only two operators are supported for nested filtering:

-   `incl`: Includes values that match all of the provided values.
-   `excl`: Excludes values that match any of the provided values.

### Usage Examples

#### Including and Excluding Values

To filter `userTags` based on included and excluded values:

`userTags.with=(name.incl(test1,test2),name.excl(test3,test4))`

This filters `userTags` to include values where `name` is both `test1` and `test2`, excluding values where `name` is either `test3` or `test4`.

Note that if you want to filter out some special character, like `#`, it must be decoded and sent like `%23`:

`userTags.with=(colorCode.incl(%23FF0000))`

### Future Enhancements

Currently, only the `incl` and `excl` operators are supported for nested filtering. However,  expanding the capabilities of the filtering system to include support for all other operators is in progress.

## Date Range Operator

The Date Range allows you to specify a range of time based on the current date. It is used with the operators `ago` and `for`. The `ago` operator calculates the range starting from the current moment and going backward, while the `for` operator calculates the range starting from the current moment and going forward.

To indicate an exact range, you can use the following units:

-   `d` for days
-   `w` for weeks
-   `m` for months
-   `y` for years

The calculation includes the first moment of the previous day, week, month, or year (for `ago`), or the last moment of the specified day, week, month, or year (for `for`). However, when the `e` modifier is added, like `de`, `we`, `me`, and `ye` the calculation starts from the current moment plus the specified number of units (calculates exact period from the current date).

| Operator | Description                             | Example               |
|----------|-----------------------------------------|-----------------------|
| ago.1d    | Range from start of previous day until now                     | createdDate=ago.1d     |
| ago.1w    | Range from start of previous week until now                    | createdDate=ago.1w     |
| ago.1m    | Range from start of previous month until now                   | createdDate=ago.1m     |
| ago.1y    | Range from start of previous year until now                    | createdDate=ago.1y     |
| ago.1de   | Range of one day ago (exact)             | createdDate=ago.1de    |
| ago.1we   | Range of one week ago (exact)            | createdDate=ago.1we    |
| ago.1me   | Range of one month ago (exact)           | createdDate=ago.1me    |
| ago.1ye   | Range of one year ago (exact)            | createdDate=ago.1ye    |
| for.1d    | Range from now until end of the next day                 | createdDate=for.1d     |
| for.1w    | Range from now until end of the next week                | createdDate=for.1w     |
| for.1m    | Range from now until end of the next month               | createdDate=for.1m     |
| for.1y    | Range from now until end of the next year                | createdDate=for.1y     |
| for.1de   | Range of one day forward (exact)         | createdDate=for.1de    |
| for.1we   | Range of one week forward (exact)        | createdDate=for.1we    |
| for.1me   | Range of one month forward (exact)       | createdDate=for.1me    |
| for.1ye   | Range of one year forward (exact)        | createdDate=for.1ye    |


## Paging Operator for Pagination

The custom paging operator allows you to perform paginated queries by specifying sorting criteria, result limit, and count settings.

### Syntax

The syntax for the custom paging operator in URL form is as follows:

`paging=(sort(<sort_criteria>),limit.<limit_value>,count.<count_value>)`

-   `<sort_criteria>`: Specify the sorting criteria for the query. The sorting criteria should be provided in the format `field_name.direction.$lastValue`, where:

    -   `field_name`: The name of the field to sort by.
    -   `direction`: The sorting direction, either `asc` for ascending or `desc` for descending.
    -   `$lastValue`: The last known value of the field from the previous page (only after initial request).
-   `<limit_value>`: The maximum number of results to retrieve per page.

-   `<count_flag>`: Specify whether to include the total count of matching records in the response. Use `true` to include the count or `false` to exclude it.

### Initial paging

To initialize the first page by certain criteria, it is enough that you put just columns with directions in `sort` criteria, in the order you would like to sort, with maximum number of rows you want to fetch with `limit`. Also, you need to privide `count` flag, depending on your need to see the count of filtered data or not:

`GET /<student_filtering_endpoint>?paging=(sort(name.asc,address.desc),limit.10,count.true)`

-   `sort(name.asc,address.desc)`: Sorting by the `name` field in ascending order, then by `address` in descending order.

-   `limit.10`: Limiting the results to a maximum of 10 per page.

-   `count.true`:  Including the total count of matching records from the response.


### Paging after initial request

To initialize the every next page by same criteria, besides columns with directions in sort criteria, you need to provide also last known values, together with primary key last value. Also, you can set the `count` flag to false, if you want to omit count this time:

`GET /<student_filtering_endpoint>?paging=(sort(name.asc.John,address.desc.New York,$key.45),limit.10,count.false)`

-   `name.asc.John`: Sorting by the `name` field in ascending order. `John`  represents the last known value of the `name` field from the previous page.

-   `address.desc.New York`: Second level sorting is in descending order by the `address` field. `New York`  represents the last known value of the `address` field from the previous page.

-   `$key.45`: Key is a placeholder for a primary key from that table you are querying, and you got it from the metadata. `45` represents the last known value of the primary key from the previous page. Note that you do not need to replace `$key` placeholder with real field name as the library will resolve it for you.

-   `limit.10`: Limiting the results to a maximum of 10 per page.

-   `count.false`:  Excluding the total count of matching records from the response.

There are a few more placeholders that you can use while setting the last value, those are `$empty` for empty strings, and `$null` for `null` values.

## Retrieve Count Only with `paging(count.true)`

To optimize your queries, you can use the `paging` operator to retrieve only the count of the data without returning the actual data rows.

### Example Usage

To retrieve the count of data without fetching the data rows, make a GET request to the desired resource endpoint and include only `paging(count.true)` in the query parameter:

`GET /<endpoint>?paging=(count.true)`

### Response

The response to the above request will include the count of the data without the actual data rows. This allows you to efficiently retrieve the count without incurring the overhead of fetching and transmitting the entire dataset, enhancing the performance of your queries.

## Operator summary table

| Operator  | Type      | Description                                             | Example Non-nested Usage       | Example Nested Usage (inside `and()`, `or()`) |
|-----------|-----------|---------------------------------------------------------|-------------------------------|----------------------------------------------|
| eq        | Comparison | Equal to                                                | `name=eq.John`                | `name.eq.John`                               |
| gt        | Comparison | Greater than                                            | `age=gt.30`                   | `age.gt.30`                                  |
| lt        | Comparison | Less than                                               | `salary=lt.50000`             | `salary.lt.50000`                            |
| gte       | Comparison | Greater than or equal to                                | `score=gte.80`                | `score.gte.80`                               |
| lte       | Comparison | Less than or equal to                                   | `count=lte.10`                | `count.lte.10`                               |
| like      | String     | Like                                                    | `name=like.Joh%`              | `name.like.Joh%`                             |
| cs        | String     | Contains                                                | `text=cs.apple`               | `text.cs.apple`                              |
| stw       | String     | Starts With                                             | `address=stw.5th`             | `address.stw.5th`                            |
| enw       | String     | Ends With                                               | `email=enw.com`               | `email.enw.com`                              |
| not.*     | String     | Logical NOT prefix for all string operators             | `email=not.like.gmail`        | `email.not.like.gmail`                       |
| and       | Logical   | Logical AND                                             | `and=(age.gte.18,state.eq.NY)`| `and(age.gte.18,state.eq.NY)`                |
| or        | Logical   | Logical OR                                              | `or=(grade=eq.A,grade=eq.B)`  | `or(grade=eq.A,grade=eq.B)`                   |
| not       | Logical   | Logical NOT                                             | `not.and=(grade.gte.6,grade.lte.8)` | `not.and(grade.gte.6,grade.lte.8)`       |
| in        | Array     | In                                                      | `category=in.(1,2,3)`         | `category.in.(1,2,3)`                         |
| not.in    | Array     | Logical NOT for the `in` operator                       | `category=not.in.(1,2,3)`      | `category.not.in.(1,2,3)`                     |
| is        | Boolean   | Is (true or false)                                      | `active=is.true`              | `active.is.true`                             |
| is.not    | Boolean   | Logical NOT for the `is` operator                       | `active=not.is.true`          | `active.not.is.true`                         |
| is        | Any       | Is $empty - none                                        | `name=is.$empty`              | `name.is.$empty`                             |
| is.not    | Any       | Is not $empty - any                                     | `name=is.not.$empty`          | `name.is.not.$empty`                         |
| ago       | Date Range | Specifies a range from start of previous period until now     | `createdDate=ago.1w`          | `createdDate.ago.1w`                          |
| for       | Date Range | Specifies a range from now until the end of the next period | `createdDate=for.1y`          | `createdDate.for.1y`                          |
| ago(exact)       | Date Range | Specifies a range of time ago from the current date     | `createdDate=ago.1we`          | `createdDate.ago.1we`                          |
| for(exact)       | Date Range | Specifies a range of time forward from the current date | `createdDate=for.1ye`          | `createdDate.for.1ye`                          |
| not.ago   | Date Range | Logical NOT for the `ago` operator                      | `createdDate=not.ago.1w`      | `createdDate.not.ago.1w`                      |
| not.for   | Date Range | Logical NOT for the `for` operator                                            | `createdDate=not.for.1m`      | `createdDate.not.for.1m`                      |
| incl/excl | Array      | Includes/excludes all values                            | `name.incl(tag_a,tag_b), name.excl(tag_c)`      | -                      |
| with      | Nested Filtering | Unfolds nested property                           | `userTags.with=(<innerConditions>)`      | -                      |

## Metadata

The metadata feature allows you to retrieve additional information about filters, columns, and subscriptions when using the library's pagination functionality. By including the `metaInclude` token in the URL, you can request the metadata in the form data.
Available options are `filters`, `columns` and `subscriptions`.

### Additional Meta Information in the Response

#### Filters Meta
When the `metaInclude=filters` token is present, the response will include the following metadata in additional information in **filtersMeta** key:

- **Data.Filters**: This field provides details about the available filters for the entity. It includes the filter options and their corresponding values.

- **Data.DefaultFilter**: This field indicates the default filter applied to the entity. It contains the filter criteria, such as name, sorting, limit, and count options.

- **Table Key**: The table key indicates the primary key or identifier used for the entity.

- **ActiveFilter**: The active filter indicates the complete filter applied to the results being fetched.

- **Hash**: The hash value represents the current default filter hash for the entity. It is recommended that the client store this value. When the client receives a new hash value, it serves as an indicator that the default filter has been changed. In such cases, the client should send a request with the "metaInclude=filters" token to ensure awareness of the changes.

When there is no `metaInclude` token, where will be basic metadata in additional info, something like:

```json
"additionalInformation": {
    "filtersMeta": {
      "activeFilter": "select=id,name,schema,activity,viewCode,subscriptionDate,countOk,countErrored,lastErrorMessage,lastOkDate,lastErrorDate&paging=(sort(subscriptionDate.desc,id.asc),limit.50,count.false)",
      "tableKey": "id",
      "hash": "BB731880"
    },
    "columnMeta": {
      "hash": "7DB628CD"
    }
  }
```

When `metaInclude=filters` token is provided, a full filter metadata response is returned:

```json
{
  "additionalInformation": {
    "filtersMeta": {
      "data": {
        "filters": [
			{
	          "id": 1,
	          "viewCode": "OJ",
	          "name": "Name filter",
	          "schema": "name=stw.tr",
	          "accessLevel": 2,
	          "setBy": "1",
	          "setDateTime": "2023-06-30T10:30:47.724671"
	        },...
		],
        "defaultFilter": {
          "id": 0,
          "viewCode": "OJ",
          "name": "",
          "schema": "paging=(sort(modifiedDate.desc,id.asc))",
          "accessLevel": 3,
          "setBy": "1",
          "setDateTime": "2023-06-30T10:30:47.724671"
        }
      },
      "hash": "F87348D7",
      "tableKey": "id"
    }
  }
}
```

#### Columns meta

When the `metaInclude=columns` token is present, the response will include the following metadata in additional information in **columnsMeta** key:

- **Data**: This field provides array of objects which contains details about the available columns for entity - name and data type.

- **Hash**: The hash value represents the current columns hash for the entity. It is recommended that the client store this value. When the client receives a new hash value, it serves as an indicator that the colums have been changed. In such cases, the client should send a request with the "metaInclude=columns" token to ensure awareness of the changes.

When `metaInclude=columns` token is provided, a full column metadata response is returned:

```json
"additionalInformation": {
    "filtersMeta": {
      "activeFilter": "metaInclude=filters",
      "tableKey": "id",
      "hash": "BB731880"
    },
    "columnMeta": {
      "data": [
        {
          "name": "activity",
          "type": "int"
        },
        {
          "name": "countErrored",
          "type": "int"
        },
        {
          "name": "countOk",
          "type": "int"
        },
        {
          "name": "id",
          "type": "int"
        },
        {
          "name": "lastErrorDate",
          "type": "DateTime"
        },
        {
          "name": "lastErrorMessage",
          "type": "string"
        },
        {
          "name": "lastOkDate",
          "type": "DateTime"
        },
        {
          "name": "name",
          "type": "string"
        },
        {
          "name": "subscriptionDate",
          "type": "DateTime"
        },
        {
          "name": "viewCode",
          "type": "string"
        }
      ],
      "hash": "7DB628CD"
    }
  }
```

#### Subscriptions meta

When the `metaInclude=subscriptions` token is present, the response will include the following metadata in additional information in **subscriptionsMeta** key:

- **countActive**: The total number of active subscriptions within the specified context. This figure indicates subscriptions that are currently active and engaged by the user.
- **countInactive**: The total number of inactive subscriptions within the specified context. This count reflects subscriptions that are currently not active or dormant from the user's perspective.


```json
"additionalInformation": {
    "filtersMeta": {
      "activeFilter": "metaInclude=filters",
      "tableKey": "id",
      "hash": "BB731880"
    },
    "columnMeta": {
      "hash": "7DB628CD"
    },
    "subscriptionsMeta": {
      "countActive": 0,
      "countInactive": 0
    }
  }
  ```

It is important to note that by leveraging the metadata feature, you can retrieve comprehensive information about filters, columns and subscriptions, track the changes in the filters and columns for improved data handling and client-side awareness.


## .NET Client Instructions

// TODO: