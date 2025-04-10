### Use Case: Request-response over Kafka topics

- Incoming HTTP web requests that involve intensive computation work, could be delegated to a set of worker services
- The worker services could pick up work from the messaging queues and crunch the numbers.
- When result is available the worker responds with a message into another queue.
- The initial sender with the open HTTP connection receives the result and resumes HTTP message processing.

See [sample](/src/Samples/README.md#sampleimages).
