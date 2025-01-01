## Changes

1. Support reply response pattern #15
2. support integration test arch and flow. #11
   * add integration test code & integration test flow. (client, trade……worker, workerpool)
   * add chaos_srcipt to ensure graceful shutdown work as our expected.
   * use docker-compose for testing.
   * simplify folder structure.
   * inject env in workflow.
   * add chaos_script
3. fix double-nack-fix issue #9
