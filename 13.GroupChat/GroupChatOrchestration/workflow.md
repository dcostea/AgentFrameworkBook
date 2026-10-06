# Workflow Diagram

```mermaid
flowchart TD
  GroupChatHost["GroupChatHost (Start)"];
  MotorsAgent_b0a634f6625241ae9426ab48579cf69c["MotorsAgent_b0a634f6625241ae9426ab48579cf69c"];
  NavigatorAgent_bbf0d573b787438292ddea8e2535c1aa["NavigatorAgent_bbf0d573b787438292ddea8e2535c1aa"];
  GroupChatHost --> MotorsAgent_b0a634f6625241ae9426ab48579cf69c;
  GroupChatHost --> NavigatorAgent_bbf0d573b787438292ddea8e2535c1aa;
  MotorsAgent_b0a634f6625241ae9426ab48579cf69c --> GroupChatHost;
  NavigatorAgent_bbf0d573b787438292ddea8e2535c1aa --> GroupChatHost;
```
