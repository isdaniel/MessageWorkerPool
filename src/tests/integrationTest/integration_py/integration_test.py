import os
import asyncio
import pika
import pytest
import pyodbc
from pika.adapters.blocking_connection import BlockingChannel
from pika.spec import Basic, BasicProperties
from typing import List
from dataclasses import dataclass
from aio_pika import connect_robust, Message

@dataclass
class RabbitMqSetting:
    port: int
    username: str
    password: str
    hostname: str

    def get_uri(self):
        return f"amqp://{self.username}:{self.password}@{self.hostname}:{self.port}"


@dataclass
class ResponseMessage:
    process_count: int
    status: str


@dataclass
class BalanceModel:
    username: str
    balance: int


class GracefulShutdownTest:

    def fetch_from_db(dsn, table_name):
        conn = pyodbc.connect(dsn)  # Ensure your DSN is set correctly
        cursor = conn.cursor()
        cursor.execute(f"SELECT UserName, Balance FROM {table_name}")
        rows = cursor.fetchall()
        conn.close()
        return rows

    @staticmethod
    async def get_all_balance_from(table_name: str) -> List[BalanceModel]:
        dsn = "Driver={ODBC Driver 17 for SQL Server};Server=127.0.0.1;Database=orleans;UID=sa;PWD=test.123;"
        loop = asyncio.get_event_loop()

        # Offload the blocking database query to an executor
        rows = await loop.run_in_executor(None, GracefulShutdownTest.fetch_from_db, dsn, table_name)

        return [BalanceModel(username=row[0], balance=row[1]) for row in rows]


    @staticmethod
    def validate_balance_comparison(act_list: List[BalanceModel], expect_list: List[BalanceModel]):
        assert len(expect_list) == len(act_list)
        assert expect_list == act_list

    async def worker_consume_message_balance_comparison_test(self):
        rabbit_mq_setting = RabbitMqSetting(
            username=os.getenv("RABBITMQ_USERNAME", "guest"),
            password=os.getenv("PASSWORD", "guest"),
            hostname=os.getenv("RABBITMQ_HOSTNAME", "127.0.0.1"),
            port=int(os.getenv("RABBITMQ_PORT", 5672))
        )

        replay_queue_name = os.getenv("REPLY_QUEUE", "integrationTesting_replyQ")
        message_received = asyncio.get_event_loop().create_future()

        async def on_message(message):
            try:
                async with message.process():
                    body = message.body.decode('utf-8')
                    print(f"IntegrationTest Finish, reply message: {body}")
                    if not message_received.done():
                        message_received.set_result(None)
            except Exception as ex:
                print(f"Error processing message: {ex}")
                if not message_received.done():
                    message_received.set_exception(ex)

        connection = await connect_robust(rabbit_mq_setting.get_uri())
        async with connection:
            channel = await connection.channel()
            await channel.set_qos(prefetch_count=1)

            queue = await channel.declare_queue(replay_queue_name, durable=True)
            await queue.consume(on_message)

            await message_received

            expected_list = await self.get_all_balance_from("dbo.Expect")
            actual_list = await self.get_all_balance_from("dbo.Act")

            self.validate_balance_comparison(actual_list, expected_list)


@pytest.mark.asyncio
async def test_worker_consume_message_balance_comparison():
    test_instance = GracefulShutdownTest()
    await test_instance.worker_consume_message_balance_comparison_test()
