
//// todo add orientation block data
//// will need to change block equals method to take that into account
//public class BirchBlock : BlockType {
//    public override bool IsSolid(Dir dir) {
//        return true;
//    }

//    public override int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
//        switch (dir) {
//            case Dir.up:
//            case Dir.down:
//                return 5;
//            default:
//                return 4;
//        }
//    }
//}