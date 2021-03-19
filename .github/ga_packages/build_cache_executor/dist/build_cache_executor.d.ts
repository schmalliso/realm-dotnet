import * as utils from "./utils/common";
/**
 * Builds and caches the resulting artifacts. In order to store the artifacts in a cache, a hash (cacheKey) is calculated over paths and the result is used as key in the cache dictionary.
 * The function can throw exceptions.
 * @param paths New line separated paths that need to be cached after the build (same paths used to create a hash)
 * @param cmds New line separated  cmds to build
 * @param oss Where to print the output messages
 * @param hashPrefix Prefix added in front of the hash that is going to be used as key in the cache dictionary
 * @param hashOptions Extra options for the default hash function
 * @param hashFunc Custom hash function if the default doesn't fullfil the user's needs
 * @returns CacheKey necessary to recover the cached build later on. Undefined is returned, otherwise.
 */
export declare function actionCore(
  paths: string,
  cmds: string,
  oss: utils.outputStream,
  hashPrefix?: string,
  hashOptions?: utils.hashOptions,
  hashFunc?: utils.hashFunc
): Promise<string | undefined>;