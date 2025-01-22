use std::cell::RefCell;
use std::collections::HashMap;
use std::io::{self, Read, Write};
use interprocess::local_socket::LocalSocketStream;
use serde::{Deserialize, Serialize};
use tokio::io::{AsyncBufReadExt, BufReader};
use tokio::task;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

pub struct MessageCommunicate;

impl MessageCommunicate {
    /// Signal to indicate closure
    pub const CLOSED_SIGNAL: &'static str = "__quit__";

    /// Signal to indicate context switch
    pub const INTERRUPT_SIGNAL: &'static str = "__context_swich__";
}

/// Message status codes
#[derive(Serialize, Deserialize, Debug)]
#[repr(i32)]
pub enum MessageStatus {
    /// Ignore message
    IgnoreMessage = -1,

    /// Message done
    MessageDone = 200,

    /// Message done with reply
    MessageDoneWithReply = 201,
}


#[derive(Serialize, Deserialize, Debug)]
pub struct MessageOutputTask {
    #[serde(rename = "0")]
    pub message: String,

    #[serde(rename = "1")]
    pub status: i32,

    #[serde(rename = "2")]
    pub headers: Option<HashMap<String, String>>,

    #[serde(rename = "3")]
    pub reply_queue_name: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct MessageInputTask {
    #[serde(rename = "0")]
    pub message: String,

    #[serde(rename = "1")]
    pub correlation_id: String,

    #[serde(rename = "2")]
    pub original_queue_name: String,

    #[serde(rename = "3")]
    pub headers: Option<HashMap<String, String>>,
}

#[derive(Debug)]
pub struct MessageProcessor {
    pub  task: Option<task::JoinHandle<()>>,
    pub pipe_stream: Arc<RefCell<LocalSocketStream>>,
    pub  close_token: Arc<AtomicBool>,
}

impl MessageProcessor {
    pub async fn read_async<TModel>(&self) -> io::Result<TModel>
    where
        TModel: for<'de> Deserialize<'de>,
    {
        let mut size_buffer = [0u8; 4];
        let mut stream = self.pipe_stream.borrow_mut();
        stream.read_exact(&mut size_buffer)?;
        let data_size = i32::from_be_bytes(size_buffer) as usize;

        let mut data_buffer = vec![0u8; data_size];
        stream.read_exact(&mut data_buffer)?;

        rmp_serde::from_slice(&data_buffer[..]).map_err(|e| io::Error::new(io::ErrorKind::Other, e))
    }

    pub async fn write_async<TModel>(&self, obj: &TModel) -> io::Result<()>
    where
        TModel: Serialize,
    {

        let data = rmp_serde::to_vec_named(obj).map_err(|e| io::Error::new(io::ErrorKind::Other, e))?;

        let data_len = data.len() as u32;
        let len_buf = data_len.to_be_bytes();

        let mut stream = self.pipe_stream.borrow_mut();
        stream.write_all(&len_buf)?;
        stream.write_all(&data )?;
        stream.flush()?;
        Ok(())
    }

    pub async fn initial_async(&mut self) {
        self.close_token = Arc::new(AtomicBool::new(false));
        let close_token = Arc::clone(&self.close_token);
        self.task = Some(task::spawn(async move {
            println!("in Task.Run...");
            let mut line = String::new();
            let stdin = tokio::io::stdin();
            let mut handle = BufReader::new(stdin);
            while handle.read_line(&mut line).await.unwrap() > 0 {
                if line.trim_end() == MessageCommunicate::CLOSED_SIGNAL {
                    close_token.store(true, Ordering::SeqCst);
                    break;
                }
                line.clear();
            }
        }));
    }

    pub async fn do_work_async<F>(&mut self, process: F)
    where
        F: Fn(MessageInputTask) -> MessageOutputTask + Send + 'static,
    {
        println!("worker starting...");
        println!("Enter text 'quit' to stop:");
        while !self.close_token.load(Ordering::SeqCst) {
            let task = self.read_async::<MessageInputTask>().await;
            if let Ok(task) = task {
                let res = process(task);
                let _ = self.write_async(&res).await;
            }
        }

        println!("loop exits!");
        if let Some(handle) = self.task.take() {
            handle.await.unwrap();
        }
    }
}
