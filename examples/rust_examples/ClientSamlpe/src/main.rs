use serde::{Deserialize, Serialize};
use serde_json;
use std::{io, thread, time};

#[derive(Serialize, Deserialize, Debug)]
struct MessageOutputTask {
    #[serde(rename = "Message")]
    message: String,
    #[serde(rename = "Status")]
    status: i32,
}

impl MessageOutputTask {
    fn new(message: String, status: i32) -> Self {
        Self { message, status }
    }

    fn to_json(&self) -> String {
        serde_json::to_string(self).unwrap_or_else(|_| "{}".to_string())
    }
}

#[derive(Deserialize, Debug)]
struct MessageInputTask {
    // Add your fields here, matching the input JSON structure
    // Example:
    #[serde(rename = "Message")]
    message: String,
}

impl MessageInputTask {
    fn from_json(message: &str) -> Option<Self> {
        serde_json::from_str(message).ok()
    }
}

struct JsonExtension;

impl JsonExtension {
    fn to_ignore_message(message: &str) -> String {
        let task = MessageOutputTask::new(message.to_string(), MessageStatus::IGNORE_MESSAGE);
        task.to_json()
    }
}

struct MessageProcessor;

impl MessageProcessor {
    fn new() -> Self {
        Self {}
    }

    fn do_work<F>(&self, process: F)
    where
        F: Fn(MessageInputTask) -> MessageOutputTask,
    {
        println!("{}", JsonExtension::to_ignore_message("worker starting..."));
        println!("{}", JsonExtension::to_ignore_message("Enter text 'quit' to stop:"));

        let stdin = io::stdin();
        loop {
            let mut input_text = String::new();
            if stdin.read_line(&mut input_text).is_err() {
                eprintln!("Failed to read input");
                continue;
            }
            let input_text = input_text.trim();
            if input_text.eq_ignore_ascii_case("quit") {
                println!("{}", JsonExtension::to_ignore_message("Exiting program."));
                break;
            }

            match MessageInputTask::from_json(input_text) {
                Some(task) => {
                    let result = process(task);
                    println!("{}", result.to_json());
                }
                None => {
                    eprintln!("Invalid task: could not parse input");
                }
            }
        }
    }
}

struct MessageStatus;

impl MessageStatus {
    const IGNORE_MESSAGE: i32 = -1;
    const MESSAGE_DONE: i32 = 200;
    const MESSAGE_DONE_WITH_REPLY: i32 = 201;
}

fn main() {
    let processor = MessageProcessor::new();

    let process_function = |task: MessageInputTask| -> MessageOutputTask {
        println!(
            "{}",
            JsonExtension::to_ignore_message(&format!(
                "this is func task.., message {}, Sleeping 1s",
                task.message
            ))
        );
        thread::sleep(time::Duration::from_secs(1));
        MessageOutputTask::new(
            "New Output Message!".to_string(),
            MessageStatus::MESSAGE_DONE,
        )
    };

    processor.do_work(process_function);
}
