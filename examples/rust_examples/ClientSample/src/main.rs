use std::cell::RefCell;
use std::error::Error;
use interprocess::local_socket::LocalSocketStream;
use std::sync::atomic::AtomicBool;
use std::sync::Arc;
use ClientSample::{MessageOutputTask,MessageProcessor, MessageStatus};
use tokio::io;
use tokio::io::{AsyncBufReadExt, BufReader};


async fn read_pipe_name() -> String {
    let mut pipe_name = String::new();
    let mut stdin = BufReader::new(io::stdin());
    stdin.read_line(&mut pipe_name)
        .await
        .expect("Failed to read line");
    //linux
    if cfg!(unix) {
        pipe_name = format!("/tmp/CoreFxPipe_{}", pipe_name);
    }
    pipe_name.trim().to_string()
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error>> {
    let pipe_name = read_pipe_name().await;
    let mut processor = MessageProcessor {
        task: None,
        pipe_stream: Arc::new(RefCell::new(LocalSocketStream::connect(pipe_name.trim()).unwrap())),
        close_token: Arc::new(AtomicBool::new(false)),
    };

    processor.initial_async().await;
    processor.do_work_async(|task| {
        println!(
            "this is func task.., message {}, Sleeping 500ms",
            task.message
        );
        std::thread::sleep(std::time::Duration::from_millis(500));
        MessageOutputTask {
            message: "New OutPut Message!".to_string(),
            status: MessageStatus::MessageDone as i32,
            headers: None,
            reply_queue_name: None,
        }
    }).await;
    Ok(())
}
