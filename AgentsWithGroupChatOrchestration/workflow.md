# Workflow Diagram

```mermaid
flowchart TD
  GroupChatHost["GroupChatHost (Start)"];
  MotorsAgent_e81f949d38a44c8196f349c91e51e6c6["MotorsAgent_e81f949d38a44c8196f349c91e51e6c6"];
  NavigatorAgent_b8a29f67ed624750979d9f35224642a8["NavigatorAgent_b8a29f67ed624750979d9f35224642a8"];
  GroupChatHost --> MotorsAgent_e81f949d38a44c8196f349c91e51e6c6;
  GroupChatHost --> NavigatorAgent_b8a29f67ed624750979d9f35224642a8;
  MotorsAgent_e81f949d38a44c8196f349c91e51e6c6 --> GroupChatHost;
  NavigatorAgent_b8a29f67ed624750979d9f35224642a8 --> GroupChatHost;
```
