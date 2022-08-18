using System;
using System.Collections.Generic;
using System.Text;

namespace CustomNetworkLib
{
    class QNode<T>
    {
        public T value;
        public QNode<T> next;

        // constructor to create
        // a new linked list node
        public QNode(T key)
        {
            this.value = key;
            this.next = null;
        }
    }

    // A class to represent a queue The queue,
    // front stores the front node of LL and
    // rear stores the last node of LL
    public class CustomQueue<T>
    {
        QNode<T> front, rear;
        object locker = new object();

        public CustomQueue()
        {
            this.front = this.rear = null;
        }

        // Method to add an key to the queue.
        public void Enqueue(T key)
        {
            lock (locker)
            {
                // Create a new LL node
                QNode<T> temp = new QNode<T>(key);

                // If queue is empty, then new
                // node is front and rear both
                if (this.rear == null)
                {
                    this.front = this.rear = temp;
                    return;
                }

                // Add the new node at the
                // end of queue and change rear
                this.rear.next = temp;
                this.rear = temp;
            }

        }

        public bool TryPeek(out T val)
        {
            lock (locker)
            {


                if (front == null)
                {
                    val = default(T);
                    return false;
                }
                val = front.value;
                return true;
            }
        }

        // Method to remove an key from queue.
        public bool TryDequeue(out T val)
        {
            lock (locker)
            {
                val = default(T);
                // If queue is empty, return NULL.
                if (this.front == null)
                    return false;

                val = front.value;
                // Store previous front and
                // move front one node ahead
                QNode<T> temp = this.front;
                this.front = this.front.next;

                // If front becomes NULL,
                // then change rear also as NULL
                if (this.front == null)
                    this.rear = null;

                return true;
            }
        }
    }


}
