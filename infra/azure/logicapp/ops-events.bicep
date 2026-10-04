// Low-code automation demo: a Consumption Logic App that reads the `ops-events`
// Service Bus queue, parses each message, branches on its `type`, and archives it
// as a JSON blob. It deliberately does NOT read `rewrite-jobs` — that queue is
// owned by the Functions worker and a second consumer would steal real jobs.

param location string = resourceGroup().location
param logicAppName string = 'replyinmyvoice-logic-dev'
param queueName string = 'ops-events'
param storageAccountName string = 'replyinmyvoicefuncdev'
param archiveContainer string = 'logicapp-inbox'
@description('Poll interval for the Service Bus trigger. Each poll is a billed connector call.')
param pollMinutes int = 5

@secure()
param serviceBusListenConnectionString string
@secure()
param storageAccountKey string

var tags = {
  project: 'replyinmyvoice'
  environment: 'dev'
  purpose: 'low-code-demo'
}

resource sbConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: '${logicAppName}-servicebus'
  location: location
  tags: tags
  properties: {
    displayName: 'ops-events listener'
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'servicebus')
    }
    parameterValues: {
      connectionString: serviceBusListenConnectionString
    }
  }
}

resource blobConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: '${logicAppName}-blob'
  location: location
  tags: tags
  properties: {
    displayName: 'logicapp-inbox archive'
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureblob')
    }
    parameterValues: {
      accountName: storageAccountName
      accessKey: storageAccountKey
    }
  }
}

resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  tags: tags
  properties: {
    state: 'Enabled'
    parameters: {
      '$connections': {
        value: {
          servicebus: {
            connectionId: sbConnection.id
            connectionName: sbConnection.name
            id: sbConnection.properties.api.id
          }
          azureblob: {
            connectionId: blobConnection.id
            connectionName: blobConnection.name
            id: blobConnection.properties.api.id
          }
        }
      }
    }
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {
        '$connections': {
          type: 'Object'
          defaultValue: {}
        }
      }
      triggers: {
        When_a_message_is_received_in_ops_events: {
          type: 'ApiConnection'
          recurrence: {
            frequency: 'Minute'
            interval: pollMinutes
          }
          splitOn: '@triggerBody()'
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'servicebus\'][\'connectionId\']'
              }
            }
            method: 'get'
            path: '/@{encodeURIComponent(encodeURIComponent(\'${queueName}\'))}/messages/batch/peek'
            queries: {
              queueType: 'Main'
              maxMessageCount: 20
              sessionId: ''
            }
          }
        }
      }
      actions: {
        Parse_event: {
          type: 'ParseJson'
          runAfter: {}
          inputs: {
            content: '@json(base64ToString(triggerBody()?[\'ContentData\']))'
            schema: {
              type: 'object'
              properties: {
                type: { type: 'string' }
                jobId: { type: 'string' }
                userId: { type: 'string' }
                detail: { type: 'string' }
                occurredAt: { type: 'string' }
              }
            }
          }
        }
        Classify_severity: {
          type: 'If'
          runAfter: {
            Parse_event: [ 'Succeeded' ]
          }
          expression: {
            or: [
              { equals: [ '@body(\'Parse_event\')?[\'type\']', 'rewrite.failed' ] }
              { equals: [ '@body(\'Parse_event\')?[\'type\']', 'billing.payment_failed' ] }
            ]
          }
          actions: {
            Mark_alert: {
              type: 'Compose'
              inputs: 'alert'
            }
          }
          else: {
            actions: {
              Mark_info: {
                type: 'Compose'
                inputs: 'info'
              }
            }
          }
        }
        Archive_to_blob: {
          type: 'ApiConnection'
          runAfter: {
            Classify_severity: [ 'Succeeded' ]
          }
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'azureblob\'][\'connectionId\']'
              }
            }
            method: 'post'
            path: '/v2/datasets/@{encodeURIComponent(encodeURIComponent(\'AccountNameFromSettings\'))}/files'
            queries: {
              folderPath: '/${archiveContainer}/@{coalesce(outputs(\'Mark_alert\'), outputs(\'Mark_info\'))}'
              name: '@{utcNow(\'yyyyMMdd-HHmmss\')}-@{triggerBody()?[\'MessageId\']}.json'
              queryParametersSingleEncoded: true
            }
            body: {
              severity: '@{coalesce(outputs(\'Mark_alert\'), outputs(\'Mark_info\'))}'
              event: '@body(\'Parse_event\')'
              serviceBus: {
                messageId: '@triggerBody()?[\'MessageId\']'
                enqueuedTimeUtc: '@triggerBody()?[\'EnqueuedTimeUtc\']'
              }
              processedBy: '@workflow()?[\'name\']'
              processedAtUtc: '@utcNow()'
            }
          }
          runtimeConfiguration: {
            contentTransfer: {
              transferMode: 'Chunked'
            }
          }
        }
        Complete_message: {
          type: 'ApiConnection'
          runAfter: {
            Archive_to_blob: [ 'Succeeded' ]
          }
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'servicebus\'][\'connectionId\']'
              }
            }
            method: 'delete'
            path: '/@{encodeURIComponent(encodeURIComponent(\'${queueName}\'))}/messages/complete'
            queries: {
              lockToken: '@triggerBody()?[\'LockToken\']'
              queueType: 'Main'
              sessionId: ''
            }
          }
        }
      }
      outputs: {}
    }
  }
}

output logicAppName string = logicApp.name
output portalDesignerUrl string = 'https://portal.azure.com/#@/resource${logicApp.id}/logicApp'
